// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using ArchPulse.Graph;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace ArchPulse.Roslyn;

/// <summary>
/// Indexes trusted .NET projects using MSBuildWorkspace + Roslyn semantics.
/// Does not execute application code or claim complete dynamic call coverage.
/// </summary>
public sealed class RoslynRepositoryIndexer
{
    public async Task<GraphSnapshot> IndexAsync(
        string entryPath, string commitSha, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commitSha) || commitSha.Length != 40 ||
            commitSha.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A real 40-character Git commit SHA is required.", nameof(commitSha));
        var fullPath = Path.GetFullPath(entryPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Project or solution not found.", fullPath);
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension is not (".sln" or ".csproj"))
            throw new ArgumentException("Only .sln/.csproj are supported.", nameof(entryPath));

        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        var root = Path.GetDirectoryName(fullPath)!;
        var graph = new GraphBuilder(commitSha.ToLowerInvariant());
        var pending = new List<(string From, string To, GraphEdgeKind Kind, GraphProvenance Evidence)>();
        using var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;
        workspace.RegisterWorkspaceFailedHandler(e => graph.AddDiagnostic("MSBuild: " + e.Diagnostic.Message));
        Solution solution;
        if (extension == ".sln")
            solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
        else
            solution = (await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken)).Solution;

        var solutionId = GraphBuilder.StableId("n", "solution:" + fullPath);
        graph.AddNode(new GraphNode(solutionId, GraphNodeKind.Solution,
            Path.GetFileName(fullPath), Evidence(commitSha, null, null)));

        foreach (var project in solution.Projects.Where(p => p.Language == LanguageNames.CSharp))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectIdentity = project.FilePath ?? project.Name;
            var projectId = GraphBuilder.StableId("n", "project:" + projectIdentity);
            graph.AddNode(new GraphNode(projectId, GraphNodeKind.Project, project.Name,
                Evidence(commitSha, null, project.FilePath == null ? null : new SourceSpan(
                    Rel(root, project.FilePath), 0, 0, 0, 0))));
            pending.Add((solutionId, projectId, GraphEdgeKind.Contains, Evidence(commitSha, null, null)));

            foreach (var document in project.Documents.Where(d => d.SupportsSyntaxTree))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var syntax = await document.GetSyntaxRootAsync(cancellationToken);
                var model = await document.GetSemanticModelAsync(cancellationToken);
                if (syntax == null || model == null)
                {
                    graph.AddDiagnostic("No semantic model: " + document.Name);
                    continue;
                }

                foreach (var declaration in syntax.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol) continue;
                    var typeId = SymbolId(symbol);
                    var typeEvidence = Evidence(commitSha, CanonicalSymbol(symbol),
                        Span(root, declaration));
                    var kind = symbol.IsRecord ? GraphNodeKind.Record : symbol.TypeKind switch
                    {
                        TypeKind.Interface => GraphNodeKind.Interface,
                        TypeKind.Struct => GraphNodeKind.Struct,
                        _ => GraphNodeKind.Class
                    };
                    graph.AddNode(new GraphNode(typeId, kind, symbol.ToDisplayString(), typeEvidence));

                    var namespaceId = GraphBuilder.StableId("n", "namespace:" +
                        symbol.ContainingAssembly.Identity + ":" + symbol.ContainingNamespace.ToDisplayString());
                    graph.AddNode(new GraphNode(namespaceId, GraphNodeKind.Namespace,
                        symbol.ContainingNamespace.IsGlobalNamespace ? "(global)" :
                        symbol.ContainingNamespace.ToDisplayString(), typeEvidence));
                    pending.Add((projectId, namespaceId, GraphEdgeKind.Contains, typeEvidence));
                    pending.Add((namespaceId, typeId, GraphEdgeKind.Contains, typeEvidence));

                    foreach (var interfaceSymbol in symbol.Interfaces)
                    {
                        var interfaceId = SymbolId(interfaceSymbol);
                        graph.AddNode(new GraphNode(interfaceId, GraphNodeKind.Interface,
                            interfaceSymbol.ToDisplayString(), typeEvidence));
                        pending.Add((typeId, interfaceId, GraphEdgeKind.Implements, typeEvidence));
                    }

                    foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (model.GetDeclaredSymbol(method, cancellationToken) is not IMethodSymbol methodSymbol) continue;
                        var methodId = SymbolId(methodSymbol);
                        var evidence = Evidence(commitSha,
                            CanonicalSymbol(methodSymbol), Span(root, method));
                        graph.AddNode(new GraphNode(methodId, GraphNodeKind.Method,
                            methodSymbol.ToDisplayString(), evidence));
                        pending.Add((typeId, methodId, GraphEdgeKind.Contains, evidence));
                    }

                    foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
                    {
                        if (model.GetDeclaredSymbol(property, cancellationToken) is not IPropertySymbol propertySymbol) continue;
                        var evidence = Evidence(commitSha,
                            CanonicalSymbol(propertySymbol), Span(root, property));
                        var propertyId = SymbolId(propertySymbol);
                        graph.AddNode(new GraphNode(propertyId, GraphNodeKind.Property,
                            propertySymbol.ToDisplayString(), evidence));
                        pending.Add((typeId, propertyId, GraphEdgeKind.Contains, evidence));
                    }
                }

                foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var declaration = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    if (declaration == null ||
                        model.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol caller) continue;
                    var info = model.GetSymbolInfo(invocation, cancellationToken);
                    if (info.Symbol is not IMethodSymbol called)
                    {
                        graph.AddDiagnostic($"Unresolved call: {Rel(root, document.FilePath ?? document.Name)}:" +
                            $"{invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}");
                        continue;
                    }
                    var callerId = SymbolId(caller);
                    var calleeId = SymbolId(called.ReducedFrom ?? called);
                    var evidence = Evidence(commitSha, CanonicalSymbol(called),
                        Span(root, invocation));
                    graph.AddNode(new GraphNode(calleeId, GraphNodeKind.ExternalDependency,
                        called.ToDisplayString(), evidence));
                    pending.Add((callerId, calleeId, GraphEdgeKind.Calls, evidence));
                }
            }
        }

        var known = graph.Build().Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (from, to, kind, evidence) in pending)
        {
            if (known.Contains(from) && known.Contains(to))
                graph.AddEdge(from, to, kind, evidence);
            else
                graph.AddDiagnostic($"Unresolved edge: {kind} {from} -> {to}");
        }
        var snapshot = graph.Build();
        var integrity = snapshot.VerifyIntegrity();
        if (integrity.Count > 0)
            throw new InvalidOperationException("Graph integrity failed: " + string.Join("; ", integrity));
        return snapshot;
    }

    private static string CanonicalSymbol(ISymbol symbol) =>
        symbol.OriginalDefinition.GetDocumentationCommentId() ??
        symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string SymbolId(ISymbol symbol) =>
        GraphBuilder.StableId("n", symbol.ContainingAssembly.Identity + ":" +
            CanonicalSymbol(symbol));

    private static GraphProvenance Evidence(string revision, string? key, SourceSpan? source) =>
        new(revision.ToLowerInvariant(), EvidenceKind.Static, "ArchPulse.Roslyn/MSBuildWorkspace", key, source);

    private static string Rel(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static SourceSpan Span(string root, SyntaxNode node)
    {
        var location = node.GetLocation().GetLineSpan();
        return new SourceSpan(Rel(root, location.Path),
            location.StartLinePosition.Line + 1, location.StartLinePosition.Character + 1,
            location.EndLinePosition.Line + 1, location.EndLinePosition.Character + 1);
    }
}
