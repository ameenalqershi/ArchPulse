// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchPulse.Graph;
using ArchPulse.Guardian;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ArchPulse.McpServer;

/// <summary>
/// Query only immutable, pre-indexed snapshots. No repository path, command,
/// agent invocation, code execution, or mutation is accepted from an MCP caller.
/// </summary>
[McpServerToolType]
public sealed class GraphTools
{
    private const int MaxUiNodes = 5000;
    private const int MaxUiEdges = 10000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [McpServerTool(Name = "archpulse_open_graph", ReadOnly = true)]
#pragma warning disable MCPEXP003
    [McpAppUi(ResourceUri = "ui://archpulse/graph")]
#pragma warning restore MCPEXP003
    [Description("Open a real source-backed .NET architecture graph in the interactive MCP Apps canvas, for an exact stored project name and revision. No example or synthetic data.")]
    public static CallToolResult OpenGraph(
        GraphSnapshotStore store,
        [Description("Exact project name in the authorized local Graph store.")] string project,
        [Description("Exact revision key used during the verified local indexing step.")] string revision)
        => LoadGraph(store, project, revision);

    [McpServerTool(Name = "archpulse_graph_snapshot", ReadOnly = true)]
    [Description("Read exact graph nodes, relations and evidence by stored project and revision, with limits and no guessed relationships.")]
    public static CallToolResult Snapshot(
        GraphSnapshotStore store, string project, string revision)
        => LoadGraph(store, project, revision);

    [McpServerTool(Name = "archpulse_symbol_context", ReadOnly = true)]
    [Description("Fetch the canonical symbol identity, location, provenance, children and directly evidenced relations for a selected node.")]
    public static CallToolResult SymbolContext(GraphSnapshotStore store,
        string project, string revision, string symbolId)
    {
        var graph = ReadValidated(store, project, revision);
        if (graph is null) return Failure("No stored graph for the specified project and revision.");
        var node = graph.Nodes.FirstOrDefault(n => n.SymbolId == symbolId);
        if (node is null) return Failure("That exact Symbol ID is absent from this revision.");
        var edges = GraphQueries.Neighbors(graph, symbolId);
        var children = graph.Nodes.Where(n => n.ParentSymbolId == symbolId)
            .OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray();
        return Success($"Exact {node.Kind} symbol: {node.SymbolId} (revision {revision}).",
            new { project, revision, symbol = node, children, edges });
    }

    [McpServerTool(Name = "archpulse_find_evidenced_path", ReadOnly = true)]
    [Description("Return the shortest directed evidence-backed static/verified call or dependency path, never a claimed runtime execution trace.")]
    public static CallToolResult Path(GraphSnapshotStore store,
        string project, string revision, string fromSymbolId, string toSymbolId)
    {
        var graph = ReadValidated(store, project, revision);
        if (graph is null) return Failure("No stored graph for the specified project and revision.");
        try
        {
            var route = GraphQueries.ShortestStaticPath(graph, fromSymbolId, toSymbolId, 5000);
            return Success($"Found {route.Count} sourced directed edges (empty also means no known path).",
                new { project, revision, fromSymbolId, toSymbolId, edges = route,
                      evidenceScope = "static-or-verified;not-a-runtime-trace" });
        }
        catch (KeyNotFoundException) { return Failure("A selected symbol does not exist in this revision."); }
        catch (InvalidOperationException) { return Failure("Graph traversal safety limit exceeded."); }
    }

    [McpServerTool(Name = "archpulse_linked_tests", ReadOnly = true)]
    [Description("Read existing Roslyn-static direct test-to-method associations for one exact source-backed Method SymbolId and stored revision. Not test execution, coverage, runner discovery or a runtime trace.")]
    public static CallToolResult LinkedTests(
        GraphSnapshotStore store, StaticTestAssociationStore testStore,
        string project, string revision, string symbolId)
    {
        var graph = ReadValidated(store, project, revision);
        if (graph is null) return Failure("No stored graph for the specified project and revision.");
        if (string.IsNullOrWhiteSpace(symbolId) || symbolId.Length > 2048 ||
            !graph.Nodes.Any(n => n.SymbolId == symbolId && n.Kind == GraphNodeKind.Method))
            return Failure("The exact source Method SymbolId is absent from this revision.");
        try
        {
            var report = testStore.Load(graph);
            var links = report?.ForTarget(symbolId);
            return Success("Read-only, static direct test mapping. No observed execution or runner outcome.",
                new { schema = "archpulse.method-test-selection/v1",
                    project, revision, targetSymbolId = symbolId,
                    mappingStatus = report is null ? "NotIndexed" :
                        links!.Count == 0 ? "Unmapped" : "MappedDirectStatic",
                    mappingScope = "RoslynStaticDirectOnly",
                    runnerDiscovery = "NotRunnerDiscovered",
                    executionStatus = "Unknown", observedCoverage = false,
                    links = links ?? [] });
        }
        catch (InvalidDataException) { return Failure("Stored test links failed source-evidence validation."); }
        catch (JsonException) { return Failure("Stored test links are malformed."); }
    }

    [McpServerTool(Name = "archpulse_feature_signals", ReadOnly = true)]
    [Description("Read exact-revision Roslyn source signals for human business-feature review. Never an approved mapping, registered runtime endpoint or observed test.")]
    public static CallToolResult FeatureSignals(
        GraphSnapshotStore graphs, FeatureDiscoveryStore discoveries,
        string project, string revision)
    {
        var graph = ReadValidated(graphs, project, revision);
        if (graph is null) return Failure("Graph revision was not stored.");
        try
        {
            var report = discoveries.Load(graph);
            if (report is null)
                return Success("Source signal discovery NotIndexed; this is not proof of zero features.",
                    new { schema = FeatureDiscoveryContract.Schema, project, revision,
                        discoveryStatus = "NotIndexed",
                        scope = FeatureDiscoveryContract.Scope, runtimeStatus = "Unknown",
                        signals = Array.Empty<FeatureCandidateSignal>() });
            if (report.Signals.Count > 500)
                return Failure("Source signals exceed the bounded MCP response; use a smaller trusted fixture.");
            return Success("Inferred business candidates only; source syntax is static, no registered route, runner result or verified business mapping.",
                new { schema = report.Schema, project, revision,
                    discoveryStatus = "IndexedCandidates", scope = report.DiscoveryScope,
                    runtimeStatus = report.RuntimeStatus,
                    signals = report.Signals, diagnostics = report.Diagnostics });
        }
        catch (InvalidDataException) { return Failure("Stored feature signals failed validation."); }
        catch (JsonException) { return Failure("Stored feature signals are unreadable."); }
    }

    [McpServerTool(Name = "archpulse_feature_scope", ReadOnly = true)]
    [Description("Read operator-curated candidate feature membership and exact existing static graph edges. This does not prove business behavior, ownership, execution or coverage.")]
    public static CallToolResult FeatureScope(
        GraphSnapshotStore graphs, FeatureCatalogStore features,
        string project, string revision, string featureId)
    {
        var graph = ReadValidated(graphs, project, revision);
        if (graph is null) return Failure("No graph at the requested exact revision.");
        if (string.IsNullOrWhiteSpace(featureId) || featureId.Length > 64)
            return Failure("An exact bounded feature slug is required.");
        try
        {
            var catalog = features.Load(graph);
            if (catalog is null) return Failure("Feature catalog NotIndexed at this revision.");
            var scope = FeatureAtlas.Project(graph, catalog, featureId);
            if (scope.Nodes.Count > MaxUiNodes || scope.StaticEdges.Count > MaxUiEdges)
                return Failure("Feature scope exceeds bounded local MCP response.");
            return Success("Manual candidate feature membership, with static internal graph edges only; no observed execution.", scope);
        }
        catch (KeyNotFoundException) { return Failure("Unknown feature ID for this revision."); }
        catch (InvalidDataException) { return Failure("Stored feature catalog failed validation."); }
        catch (JsonException) { return Failure("Stored feature catalog is malformed."); }
    }

    [McpServerTool(Name = "archpulse_guardian_check", ReadOnly = true)]
    [Description("Evaluate a proposed forbidden cross-project relation against the exact saved graph. This is not policy approval or enforcement.")]
    public static CallToolResult GuardianCheck(GraphSnapshotStore store, string project, string revision,
        string fromProject, string toProject, string relation)
    {
        var graph = ReadValidated(store, project, revision);
        if (graph is null) return Failure("No stored graph for the specified project and revision.");
        var rule = new ForbiddenDependencyRule("ADHOC-MCP-CHECK", "experimental-v1",
            fromProject, toProject, relation, RuleSeverity.Warning);
        try
        {
            var results = ArchitectureGuardian.Evaluate(graph, [rule]);
            return Success("Read-only architecture policy evaluation; no project policy was changed.",
                new { project, revision, results });
        }
        catch (ArgumentException) { return Failure("A concrete project and relation are required."); }
    }

    internal static CallToolResult LoadGraph(GraphSnapshotStore store, string project, string revision)
    {
        var graph = ReadValidated(store, project, revision);
        if (graph is null) return Failure("No stored graph for this project and revision; run the trusted local CLI first.");
        if (graph.Nodes.Count > MaxUiNodes || graph.Edges.Count > MaxUiEdges)
            return Failure("Graph exceeds the bounded local MCP response. Use a smaller repository slice.");
        return Success($"Loaded source-backed graph {project} at {revision}: {graph.Nodes.Count} nodes and {graph.Edges.Count} edges. " +
                       "Evidence modes are explicit and are not live runtime traces.", graph);
    }

    private static GraphSnapshot? ReadValidated(GraphSnapshotStore store, string project, string revision)
    {
        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(revision))
            return null;
        if (project.Length > 160 || revision.Length > 160)
            return null;
        return store.Load(project, revision);
    }

    internal static CallToolResult Success<T>(string text, T value) => new()
    {
        Content = [new TextContentBlock { Text = text }],
        StructuredContent = JsonSerializer.SerializeToElement(value, JsonOptions)
    };

    internal static CallToolResult Failure(string reason) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = reason }]
    };
}
