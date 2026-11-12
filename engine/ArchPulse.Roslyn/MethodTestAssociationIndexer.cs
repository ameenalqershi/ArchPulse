// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchPulse.Roslyn;

/// <summary>
/// Static direct-invocation evidence between a framework-annotated test method declaration
/// and a source-backed SUT method in a different project. It is NOT runtime test discovery,
/// executed coverage, a test runner case, or an observed invocation.
/// </summary>
public static class MethodTestAssociationIndexer
{
    public const string Schema = "archpulse.method-test-associations/v1";

    private static readonly IReadOnlyDictionary<string, string> KnownAttributes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Xunit.FactAttribute"] = "xUnit",
            ["Xunit.TheoryAttribute"] = "xUnit",
            ["NUnit.Framework.TestAttribute"] = "NUnit",
            ["NUnit.Framework.TestCaseAttribute"] = "NUnit",
            ["Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"] = "MSTest",
            ["Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute"] = "MSTest"
        };

    public static StaticTestAssociationReport Project(
        GraphSnapshot graph,
        IEnumerable<(string ProjectName, CSharpCompilation Compilation)> projectCompilations)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(projectCompilations);
        GraphSnapshotStore.Validate(graph);

        var nodeById = graph.Nodes.ToDictionary(x => x.SymbolId, StringComparer.Ordinal);
        var callsByCaller = graph.Edges
            .Where(e => e.Relation == "Calls" && e.Evidence.Kind == EvidenceKind.Static)
            .GroupBy(e => e.FromSymbolId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var declarations = new List<StaticTestDeclaration>();
        var links = new List<StaticMethodTestLink>();
        var diagnostics = new List<string>();

        foreach (var (project, compilation) in projectCompilations
                     .OrderBy(p => p.ProjectName, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(project))
                throw new ArgumentException("Test project name must be provided.");
            if (!nodeById.ContainsKey("project:" + project))
                throw new InvalidDataException("The test project is absent from this graph.");
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var syntax in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(syntax) is not IMethodSymbol method)
                        continue;
                    var framework = method.GetAttributes()
                        .Select(a => a.AttributeClass?.ToDisplayString())
                        .Where(a => a is not null && KnownAttributes.ContainsKey(a))
                        .Select(a => KnownAttributes[a!])
                        .OrderBy(f => f, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (framework is null)
                        continue;

                    var id = SymbolId(project, method);
                    if (!nodeById.TryGetValue(id, out var testNode) ||
                        testNode.Kind != GraphNodeKind.Method)
                    {
                        diagnostics.Add("Test declaration not backed by graph node: " + id);
                        continue;
                    }
                    declarations.Add(new StaticTestDeclaration(id, project, framework,
                        testNode.Evidence, "KnownAttributeNameUnverifiedByRunner",
                        "TestMethodDeclarationNotParameterizedCase", "NotRun"));

                    if (!callsByCaller.TryGetValue(id, out var sourceEdges))
                        continue;

                    foreach (var invocation in syntax.DescendantNodes()
                                 .OfType<InvocationExpressionSyntax>())
                    {
                        // Calls under delegates/lambdas/local functions are NOT direct invocations
                        // by the test method body; do not infer that callbacks were executed.
                        if (invocation.Ancestors().TakeWhile(a => a != syntax)
                            .Any(a => a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                            continue;
                        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol called)
                            continue;
                        var targetDoc = (called.ReducedFrom ?? called.OriginalDefinition)
                            .GetDocumentationCommentId();
                        if (targetDoc is null)
                            continue;

                        var point = invocation.GetLocation().GetLineSpan().StartLinePosition;
                        foreach (var edge in sourceEdges.Where(e =>
                                     e.Evidence.Line == point.Line + 1 &&
                                     e.Evidence.Column == point.Character + 1 &&
                                     e.ToSymbolId.EndsWith("::" + targetDoc, StringComparison.Ordinal)))
                        {
                            if (!nodeById.TryGetValue(edge.ToSymbolId, out var targetNode) ||
                                targetNode.Kind != GraphNodeKind.Method)
                                continue;
                            // A helper in the test project is not presumed to be the tested SUT.
                            if (edge.ToSymbolId.StartsWith(project + "::", StringComparison.Ordinal))
                                continue;
                            links.Add(new StaticMethodTestLink(id, edge.ToSymbolId, project,
                                framework, "DirectInvocation", "StaticSourceOnly",
                                "Unobserved", "Unknown", edge.Evidence));
                        }
                    }
                }
            }
        }

        // A test can call the same SUT method multiple times, but it is one method/test
        // association with multiple reproducible call-site evidence receipts.
        var grouped = links.GroupBy(x => (x.TestMethodSymbolId, x.TargetSymbolId, x.TestProject,
                x.Framework, x.Relation, x.Verification, x.ExecutionEvidence, x.LastResult))
            .Select(g => new StaticMethodTestAssociation(g.Key.TestMethodSymbolId,
                g.Key.TargetSymbolId, g.Key.TestProject, g.Key.Framework,
                g.Key.Relation, g.Key.Verification, g.Key.ExecutionEvidence,
                g.Key.LastResult,
                g.Select(e => e.CallSite).Distinct()
                    .OrderBy(e => e.RelativeFile, StringComparer.Ordinal)
                    .ThenBy(e => e.Line).ThenBy(e => e.Column).ToArray()))
            .OrderBy(a => a.TargetSymbolId, StringComparer.Ordinal)
            .ThenBy(a => a.TestMethodSymbolId, StringComparer.Ordinal).ToArray();

        return new StaticTestAssociationReport(Schema, graph.Project, graph.Revision,
            "RoslynStaticDirectOnly", "NotRunnerDiscovered",
            "NoRunOrCoverageReceipts", declarations
                .OrderBy(d => d.TestMethodSymbolId, StringComparer.Ordinal).ToArray(),
            grouped, diagnostics.Distinct(StringComparer.Ordinal)
                .OrderBy(d => d, StringComparer.Ordinal).ToArray());
    }

    private static string SymbolId(string projectName, IMethodSymbol method) =>
        projectName + "::" + (method.GetDocumentationCommentId()
            ?? method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}
