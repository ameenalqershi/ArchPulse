// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace ArchPulse.Roslyn;

/// <summary>
/// Loads an authorized .NET solution with real MSBuild evaluated project references and
/// Roslyn semantic compilations. Evaluation of untrusted project files is unsafe.
/// This component must be run only on an approved workspace or inside an isolated sandbox.
/// </summary>
public sealed class SolutionGraphIndexer
{
    private static readonly object RegistrationGate = new();

    public async Task<GraphSnapshot> IndexSolutionAsync(
        string solutionPath, string revision, CancellationToken cancellationToken = default) =>
        (await AnalyzeSolutionAsync(solutionPath, revision, cancellationToken)).Graph;

    /// <summary>
    /// Indexes the same source-backed graph and projects direct static test links.
    /// Test metadata is only an annotated declaration; no runner or coverage is inferred.
    /// </summary>
    public async Task<SolutionGraphAnalysis> AnalyzeSolutionAsync(
        string solutionPath, string revision, CancellationToken cancellationToken = default)
    {
        solutionPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException("The solution does not exist.", solutionPath);
        if (string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("The exact source revision is required.", nameof(revision));
        if (Path.GetExtension(solutionPath) is not (".sln" or ".slnx"))
            throw new ArgumentException("Only .sln/.slnx solution inputs are accepted.", nameof(solutionPath));

        lock (RegistrationGate)
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
        }

        using var workspace = MSBuildWorkspace.Create();
        var diagnostics = new List<string>();
        workspace.WorkspaceFailed += (_, failure) =>
        {
            lock (diagnostics)
                diagnostics.Add($"MSBuild workspace {failure.Diagnostic.Kind}: {failure.Diagnostic.Message}");
        };

        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        var projects = solution.Projects.Where(p => p.Language == LanguageNames.CSharp)
            .OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
        if (projects.Length == 0)
            throw new InvalidDataException("No C# projects loaded: " + string.Join("; ", diagnostics));
        if (projects.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != projects.Length)
            throw new InvalidDataException("Projects have duplicate names; disambiguate before indexing.");

        var assemblies = new Dictionary<string, string>(StringComparer.Ordinal);
        var compilations = new List<(Project Project, CSharpCompilation Compilation)>();
        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await project.GetCompilationAsync(cancellationToken) is not CSharpCompilation compilation)
            {
                diagnostics.Add($"No C# compilation for {project.Name}; project was not silently indexed.");
                continue;
            }
            var assemblyName = compilation.AssemblyName ?? project.AssemblyName ?? project.Name;
            if (!assemblies.TryAdd(assemblyName, project.Name))
                throw new InvalidDataException($"Ambiguous assembly identity: {assemblyName}.");
            compilations.Add((project, compilation));
        }

        if (compilations.Count != projects.Length)
            throw new InvalidDataException("At least one project compilation was unavailable. " +
                                           string.Join("; ", diagnostics));

        string root = Path.GetDirectoryName(solutionPath)!;
        string solutionId = $"solution:{Path.GetFileNameWithoutExtension(solutionPath)}";
        var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal)
        {
            [solutionId] = new(solutionId, GraphNodeKind.Solution,
                Path.GetFileName(solutionPath), null,
                new SourceEvidence(revision, Path.GetFileName(solutionPath), 0, 0, EvidenceKind.Static))
        };
        var edges = new Dictionary<string, GraphEdge>(StringComparer.Ordinal);

        foreach (var (project, compilation) in compilations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = StaticGraphIndexer.IndexCompilation(project.Name, compilation, revision, assemblies);
            foreach (var sourceNode in snapshot.Nodes)
            {
                var node = sourceNode with
                {
                    ParentSymbolId = sourceNode.Kind == GraphNodeKind.Project
                        ? solutionId : sourceNode.ParentSymbolId,
                    Evidence = Normalize(sourceNode.Evidence, root)
                };
                if (!nodes.TryAdd(node.SymbolId, node))
                    throw new InvalidDataException($"Duplicate symbol identity: {node.SymbolId}");
            }
            foreach (var edge in snapshot.Edges)
            {
                var normalized = edge with { Evidence = Normalize(edge.Evidence, root) };
                if (!edges.TryAdd(edge.EdgeId, normalized))
                    throw new InvalidDataException($"Duplicate call edge identity: {edge.EdgeId}");
            }
            diagnostics.AddRange(snapshot.Diagnostics.Select(d => $"{project.Name}: {d}"));
        }

        foreach (var (project, _) in compilations)
        {
            foreach (var reference in project.ProjectReferences)
            {
                var target = solution.GetProject(reference.ProjectId);
                if (target is null || !projects.Contains(target))
                {
                    diagnostics.Add($"{project.Name}: unresolved project reference.");
                    continue;
                }
                string from = $"project:{project.Name}", to = $"project:{target.Name}";
                string id = $"dependency:{from}:{to}";
                string file = project.FilePath is null
                    ? "" : Path.GetRelativePath(root, project.FilePath).Replace('\\', '/');
                edges[id] = new GraphEdge(id, from, to, "DependsOn",
                    new SourceEvidence(revision, file, 0, 0, EvidenceKind.Static));
            }
        }

        // Scope the graph to verified declarations in the loaded solution.
        // No dangling or externally presumed C# edge is accepted as a fact.
        foreach (var edge in edges.Values.ToArray())
        {
            if (nodes.ContainsKey(edge.FromSymbolId) && nodes.ContainsKey(edge.ToSymbolId))
                continue;
            diagnostics.Add($"Dropped edge with missing source declaration: {edge.EdgeId}");
            edges.Remove(edge.EdgeId);
        }
        var graph = new GraphSnapshot(Path.GetFileNameWithoutExtension(solutionPath), revision,
            nodes.Values.OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray(),
            edges.Values.OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray(),
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        var testLinks = MethodTestAssociationIndexer.Project(graph,
            compilations.Select(c => (c.Project.Name, c.Compilation)));
        var featureSignals = FeatureCandidateIndexer.Discover(graph, testLinks,
            compilations.Select(c => (c.Project.Name, c.Compilation)));
        var dataJourney = LocalDataJourneyAnalyzer.Analyze(graph,
            compilations.Select(c => (c.Project.Name, c.Compilation)), root);
        return new SolutionGraphAnalysis(graph, testLinks, featureSignals, dataJourney);
    }

    private static SourceEvidence Normalize(SourceEvidence evidence, string root)
    {
        var file = evidence.RelativeFile;
        if (string.IsNullOrEmpty(file)) return evidence;
        return evidence with
        {
            RelativeFile = (Path.IsPathRooted(file)
                ? Path.GetRelativePath(root, file) : file).Replace('\\', '/')
        };
    }
}


// An additive analysis result; graph storage and its canonical node/edge structure
// are unchanged. Test declarations do not become standalone canvas nodes.
public sealed record SolutionGraphAnalysis(
    GraphSnapshot Graph, StaticTestAssociationReport TestAssociations,
    FeatureDiscoveryReport FeatureSignals, LocalDataJourneyReport DataJourney);
