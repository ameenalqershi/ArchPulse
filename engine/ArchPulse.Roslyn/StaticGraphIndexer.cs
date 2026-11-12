// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchPulse.Roslyn;

/// <summary>
/// A bounded, source-derived .NET indexer. It does not pretend to resolve external
/// dependencies absent from the supplied compilation and does not claim runtime observation.
/// </summary>
public static class StaticGraphIndexer
{
    public static GraphSnapshot IndexDirectory(string projectName, string sourceRoot, string revision)
    {
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException(sourceRoot);
        var sources = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(sourceRoot, file)
                .Split(Path.DirectorySeparatorChar)
                .Any(component => component is "bin" or "obj" or ".git"))
            .ToDictionary(file => Path.GetRelativePath(sourceRoot, file).Replace('\\', '/'),
                File.ReadAllText, StringComparer.Ordinal);
        return IndexSources(projectName, sources, revision);
    }

    public static GraphSnapshot IndexSources(string projectName,
        IReadOnlyDictionary<string, string> sources, string revision)
    {
        if (string.IsNullOrWhiteSpace(projectName)) throw new ArgumentException("Project name is required.");
        if (string.IsNullOrWhiteSpace(revision)) throw new ArgumentException("Explicit source revision is required.");
        var syntaxTrees = sources.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => CSharpSyntaxTree.ParseText(pair.Value, path: pair.Key))
            .ToArray();

        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        var references = (trustedPlatformAssemblies ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(file => MetadataReference.CreateFromFile(file));
        var compilation = CSharpCompilation.Create(
            projectName, syntaxTrees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return IndexCompilation(projectName, compilation, revision);
    }

    /// <summary>Index a semantic compilation with explicit known project assemblies.</summary>
    public static GraphSnapshot IndexCompilation(string projectName, CSharpCompilation compilation,
        string revision, IReadOnlyDictionary<string, string>? knownProjectAssemblies = null)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("Project name is required.", nameof(projectName));
        if (string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("Explicit source revision is required.", nameof(revision));
        var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var edges = new Dictionary<string, GraphEdge>(StringComparer.Ordinal);
        var diagnostics = new List<string>();
        string projectId = $"project:{projectName}";
        nodes.Add(projectId, new GraphNode(projectId, GraphNodeKind.Project, projectName, null,
            new SourceEvidence(revision, "", 0, 0, EvidenceKind.Static)));

        var methods = new List<(MethodDeclarationSyntax Syntax, IMethodSymbol Symbol, SemanticModel Model)>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) continue;
                var namespaceName = symbol.ContainingNamespace?.IsGlobalNamespace == false
                    ? symbol.ContainingNamespace.ToDisplayString()
                    : "(global)";
                var namespaceId = $"namespace:{projectName}:{namespaceName}";
                nodes.TryAdd(namespaceId, new GraphNode(namespaceId, GraphNodeKind.Namespace,
                    namespaceName, projectId, Location(tree, declaration.SpanStart, revision)));
                var parent = symbol.ContainingType is null ? namespaceId : SymbolId(projectName, symbol.ContainingType);
                var typeKind = declaration switch
                {
                    InterfaceDeclarationSyntax => GraphNodeKind.Interface,
                    StructDeclarationSyntax => GraphNodeKind.Struct,
                    RecordDeclarationSyntax => GraphNodeKind.Record,
                    EnumDeclarationSyntax => GraphNodeKind.Enum,
                    _ => GraphNodeKind.Class
                };
                var id = SymbolId(projectName, symbol);
                nodes.TryAdd(id, new GraphNode(id, typeKind,
                    symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    parent, Location(tree, declaration.SpanStart, revision)));
            }

            foreach (var declaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not IMethodSymbol symbol) continue;
                var id = SymbolId(projectName, symbol);
                nodes.TryAdd(id, new GraphNode(id, GraphNodeKind.Method,
                    symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    SymbolId(projectName, symbol.ContainingType),
                    Location(tree, declaration.SpanStart, revision)));
                methods.Add((declaration, symbol, model));
            }

            foreach (var declaration in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not IPropertySymbol symbol) continue;
                var id = SymbolId(projectName, symbol);
                nodes.TryAdd(id, new GraphNode(id, GraphNodeKind.Property,
                    symbol.Name, SymbolId(projectName, symbol.ContainingType),
                    Location(tree, declaration.SpanStart, revision)));
            }
        }

        foreach (var (method, symbol, model) in methods)
        {
            var ordinalByTarget = new Dictionary<string, int>(StringComparer.Ordinal);
            string sourceId = SymbolId(projectName, symbol);
            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                // Do not infer that overload candidates or unknown external APIs are resolved.
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invoked)
                {
                    var location = invocation.GetLocation().GetLineSpan();
                    diagnostics.Add($"Unknown call: {location.Path}:{location.StartLinePosition.Line + 1}");
                    continue;
                }
                string? targetProject = string.Equals(invoked.ContainingAssembly?.Name,
                    compilation.AssemblyName, StringComparison.Ordinal)
                    ? projectName
                    : knownProjectAssemblies is not null
                      && knownProjectAssemblies.TryGetValue(invoked.ContainingAssembly?.Name ?? "", out var known)
                        ? known : null;
                if (targetProject is null)
                    continue; // No invented internal edges for libraries not present in this workspace.
                string targetId = SymbolId(targetProject, invoked.ReducedFrom ?? invoked.OriginalDefinition);
                if (targetProject == projectName && !nodes.ContainsKey(targetId))
                    continue; // Symbol absent from source declarations (e.g. external metadata).
                int ordinal = ordinalByTarget.GetValueOrDefault(targetId) + 1;
                ordinalByTarget[targetId] = ordinal;
                string edgeId = $"call:{sourceId}:{targetId}:{ordinal}";
                edges.TryAdd(edgeId, new GraphEdge(edgeId, sourceId, targetId, "Calls",
                    Location(invocation.SyntaxTree, invocation.SpanStart, revision)));
            }
        }

        foreach (var diagnostic in compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Take(100))
            diagnostics.Add(diagnostic.ToString());

        return new GraphSnapshot(projectName, revision,
            nodes.Values.OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray(),
            edges.Values.OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray(),
            diagnostics);
    }

    private static string SymbolId(string projectName, ISymbol symbol) =>
        $"{projectName}::{symbol.GetDocumentationCommentId()
            ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";

    private static SourceEvidence Location(SyntaxTree tree, int spanStart, string revision)
    {
        var line = tree.GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(spanStart, 0));
        return new SourceEvidence(revision, line.Path,
            line.StartLinePosition.Line + 1, line.StartLinePosition.Character + 1, EvidenceKind.Static);
    }
}
