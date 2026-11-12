// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using ArchPulse.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchPulse.Roslyn;

/// <summary>
/// Conservative source-signal discovery. Recognizes only named controller HTTP
/// attributes, conventionally named Handler.Handle methods and the existing
/// test declaration index. It NEVER promotes a suggestion to feature membership.
/// </summary>
public static partial class FeatureCandidateIndexer
{
    private static readonly IReadOnlyDictionary<string, string> MvcAttributes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.AspNetCore.Mvc.HttpGetAttribute"] = "GET",
            ["Microsoft.AspNetCore.Mvc.HttpPostAttribute"] = "POST",
            ["Microsoft.AspNetCore.Mvc.HttpPutAttribute"] = "PUT",
            ["Microsoft.AspNetCore.Mvc.HttpDeleteAttribute"] = "DELETE",
            ["Microsoft.AspNetCore.Mvc.HttpPatchAttribute"] = "PATCH"
        };

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex PascalBreak();

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlug();

    public static FeatureDiscoveryReport Discover(
        GraphSnapshot graph,
        StaticTestAssociationReport tests,
        IEnumerable<(string ProjectName, CSharpCompilation Compilation)> projectCompilations)
    {
        GraphSnapshotStore.Validate(graph);
        StaticTestAssociationStore.Validate(graph, tests);
        ArgumentNullException.ThrowIfNull(projectCompilations);
        var nodes = graph.Nodes.Where(n => n.Kind == GraphNodeKind.Method)
            .ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        var signals = new Dictionary<string, FeatureCandidateSignal>(StringComparer.Ordinal);
        var diagnostics = new List<string>();
        foreach (var (project, compilation) in projectCompilations
                     .OrderBy(x => x.ProjectName, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(project) || !graph.Nodes.Any(n =>
                    n.SymbolId == "project:" + project && n.Kind == GraphNodeKind.Project))
                throw new InvalidDataException("Signal discovery compilation is absent from exact graph.");
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var syntax in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(syntax) is not IMethodSymbol method)
                        continue;
                    var symbolId = project + "::" + (method.GetDocumentationCommentId()
                        ?? method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    if (!nodes.TryGetValue(symbolId, out var node) ||
                        node.Evidence.Kind != EvidenceKind.Static)
                        continue;

                    // This name/type match is a DISCOVERY hint, not proof that an
                    // ASP.NET host registered the controller or that filters ran.
                    if (method.ContainingType.Name.EndsWith("Controller", StringComparison.Ordinal))
                    {
                        foreach (var attribute in method.GetAttributes())
                        {
                            if (attribute.AttributeClass is null ||
                                !MvcAttributes.TryGetValue(attribute.AttributeClass.ToDisplayString(),
                                    out var verb))
                                continue;
                            string? route = null;
                            if (attribute.ConstructorArguments.Length > 0 &&
                                attribute.ConstructorArguments[0].Value is string value)
                            {
                                if (value.Length > 256 || value.Any(char.IsControl))
                                {
                                    diagnostics.Add("Skipped unsafe or excessive route hint on " +
                                                    Truncate(symbolId, 200));
                                    continue;
                                }
                                route = value;
                            }
                            Add(FeatureSignalKind.ControllerAction, symbolId,
                                method.Name, node, "KnownMvcHttpAttributeName;NotRegisteredRoute",
                                "Known HTTP attribute type name; host registration, effective route " +
                                "and business intent are not verified.", verb, route);
                            break; // Same source action may contain several verb attributes.
                        }
                    }

                    // Syntactic convention only; not proof of MediatR/DI registration.
                    if (method.ContainingType.Name.EndsWith("Handler", StringComparison.Ordinal) &&
                        method.Name is "Handle" or "HandleAsync")
                        Add(FeatureSignalKind.NamedHandler, symbolId,
                            method.ContainingType.Name[..^"Handler".Length], node,
                            "HandlerTypeAndMethodNaming;NotRuntimeRegistration",
                            "Handler class/method naming convention; interface, DI wiring and " +
                            "business semantics are not established.", null, null);
                }
            }
        }

        foreach (var test in tests.TestDeclarations)
        {
            if (!nodes.TryGetValue(test.TestMethodSymbolId, out var node))
                continue;
            var name = node.Label.Split('(')[0];
            Add(FeatureSignalKind.TestDeclaration, test.TestMethodSymbolId,
                name, node, "KnownTestAttributeName;NotRunnerDiscovered",
                "Known source test attribute name; not a discovered test case, " +
                "observed run, SUT coverage, or business acceptance proof.", null, null);
        }

        var report = new FeatureDiscoveryReport(
            FeatureDiscoveryContract.Schema, graph.Project, graph.Revision,
            FeatureDiscoveryContract.Scope, FeatureDiscoveryContract.RuntimeStatus,
            signals.Values.OrderBy(s => s.SignalId, StringComparer.Ordinal).ToArray(),
            diagnostics.OrderBy(s => s, StringComparer.Ordinal).Take(500).ToArray());
        FeatureDiscoveryContract.Validate(graph, report);
        return report;

        void Add(FeatureSignalKind kind, string symbolId, string hint,
            GraphNode node, string detector, string explanation, string? verb, string? route)
        {
            var suggestion = ToSlug(hint);
            if (suggestion is null) return;
            var signal = new FeatureCandidateSignal(
                $"feature-signal:{kind}:{symbolId}", kind, symbolId, suggestion,
                detector, explanation, node.Evidence, EvidenceKind.Inferred, verb, route);
            signals.TryAdd(signal.SignalId, signal);
            if (signals.Count > 5_000)
                throw new InvalidDataException("Feature discovery signal cap exceeded; index smaller projects.");
        }
    }

    private static string? ToSlug(string name)
    {
        var result = NonSlug().Replace(PascalBreak().Replace(name, "$1-$2")
            .ToLowerInvariant(), "-").Trim('-');
        if (result.Length > 64) result = result[..64].TrimEnd('-');
        return result.Length is >= 3 and <= 64 && result[0] is >= 'a' and <= 'z'
            ? result : null;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
