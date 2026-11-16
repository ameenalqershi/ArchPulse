// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Guardian;
using Xunit;

namespace ArchPulse.Guardian.Tests;

public sealed class AggregatedBoundaryPlannerTests
{
    private static GraphSnapshot MakeGraph(int count = 250, string revision = "r1")
    {
        var e = new SourceEvidence(revision, "src/Calls.cs", 5, 2, EvidenceKind.Static);
        var outside = e with { Kind = EvidenceKind.Observed };
        var nodes = new List<GraphNode>
        {
            new("root", GraphNodeKind.Solution, "System", null, e),
            new("inside", GraphNodeKind.Project, "Inside", "root", e),
            new("outside", GraphNodeKind.Project, "Outside", "root", e)
        };
        var edges = new List<GraphEdge>();
        for (int i = 0; i < count; i++)
        {
            string a = $"in:{i:D4}";
            string b = $"out:{i:D4}";
            nodes.Add(new(a, GraphNodeKind.Method, a, "inside", e));
            nodes.Add(new(b, GraphNodeKind.Method, b, "outside", outside));
            edges.Add(new($"call:{i:D4}", a, b, "Calls", e));
        }
        return new GraphSnapshot("Demo", revision, nodes, edges, []);
    }

    private static ArchitectureFinding Finding(GraphEdge edge,
        RuleOutcome outcome = RuleOutcome.Violation,
        RuleSeverity severity = RuleSeverity.Blocker) =>
        new("NO-CALL", "v1", severity, outcome, edge.EdgeId,
            edge.FromSymbolId, edge.ToSymbolId, edge.Evidence, "Test-only local policy finding.");

    [Fact]
    public void Collapse250CallsRetainsAllCountsAndHiddenBlocker()
    {
        var graph = MakeGraph();
        var blocker = Finding(graph.Edges[^1]);
        var summary = AggregatedBoundaryPlanner.Plan(graph, "inside", [blocker]);
        Assert.Equal(AggregatedBoundaryPlan.ContractSchema, summary.Schema);
        Assert.Equal("r1", summary.Revision);
        Assert.Equal(250, summary.TotalBoundaryEdges);
        Assert.Equal("AuthorizationNotEvaluated", summary.AccessStatus);
        var bundle = Assert.Single(summary.Bundles);
        Assert.Equal(new AggregatedBoundaryKey("Outbound", "Calls", "outside"), bundle.Key);
        Assert.Equal(250, bundle.SourceEdgeCount);
        Assert.Equal(250, bundle.EvidenceCounts.Single(x => x.Kind == EvidenceKind.Static).Count);
        Assert.Equal(0, bundle.EvidenceCounts.Single(x => x.Kind == EvidenceKind.Observed).Count);
        Assert.Equal(1, bundle.ViolationEdgeCount);
        Assert.Equal(1, bundle.BlockingViolationEdgeCount);
        Assert.Equal("CallerSuppliedFindingsUnverified", bundle.PolicyEvidenceStatus);
        Assert.Equal(20, bundle.PreviewWitnessEdgeIds.Count);
        Assert.DoesNotContain("call:0249", bundle.PreviewWitnessEdgeIds);
        Assert.True(bundle.HasMoreWitnesses);

        var ids = new List<string>();
        string? cursor = null;
        do
        {
            var page = AggregatedBoundaryPlanner.WitnessesPage(graph, "inside", bundle.Key,
                pageSize: 100, afterEdgeId: cursor);
            Assert.Equal("r1", page.Revision);
            Assert.Equal(250, page.TotalWitnesses);
            Assert.All(page.Witnesses, e => Assert.Equal("r1", e.Evidence.Revision));
            ids.AddRange(page.Witnesses.Select(x => x.EdgeId));
            cursor = page.NextAfterEdgeId;
        } while (cursor is not null);
        Assert.Equal(250, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("call:0000", ids[0]);
        Assert.Equal("call:0249", ids[^1]);
    }

    [Fact]
    public void DoesNotConflateDirectionRelationOrEvidenceModes()
    {
        var graph = MakeGraph(2);
        var e = graph.Edges[0].Evidence;
        graph = graph with
        {
            Edges = graph.Edges.Concat([
                new GraphEdge("incoming", "out:0000", "in:0000", "Calls",
                    e with { Kind = EvidenceKind.Inferred }),
                new GraphEdge("dependency", "in:0000", "out:0000", "DependsOn",
                    e with { Kind = EvidenceKind.Observed }),
                new GraphEdge("internal", "in:0000", "in:0001", "Calls", e),
                new GraphEdge("external", "out:0000", "out:0001", "Calls", e)
            ]).ToArray()
        };
        var result = AggregatedBoundaryPlanner.Plan(graph, "inside");
        Assert.Equal(4, result.TotalBoundaryEdges);
        Assert.Equal(3, result.Bundles.Count);
        Assert.All(result.Bundles, g =>
        {
            Assert.Equal("NotEvaluated", g.PolicyEvidenceStatus);
            Assert.Null(g.ViolationEdgeCount);
            Assert.Null(g.BlockingViolationEdgeCount);
        });
        Assert.Equal(1, result.Bundles.Single(x => x.Key.Direction == "Inbound")
            .EvidenceCounts.Single(x => x.Kind == EvidenceKind.Inferred).Count);
        Assert.Equal(1, result.Bundles.Single(x => x.Key.Relation == "DependsOn")
            .EvidenceCounts.Single(x => x.Kind == EvidenceKind.Observed).Count);
    }

    [Fact]
    public void CountPolicyFindingsPerEdgeNotPerRuleAndKeepUnknownSeparate()
    {
        var graph = MakeGraph(3);
        var edges = graph.Edges;
        var findings = new[]
        {
            Finding(edges[0]), Finding(edges[0]) with { RuleId = "OTHER" },
            Finding(edges[1], RuleOutcome.Unknown),
            Finding(edges[2], RuleOutcome.Waived)
        };
        var bundle = Assert.Single(AggregatedBoundaryPlanner.Plan(graph, "inside", findings).Bundles);
        Assert.Equal(1, bundle.ViolationEdgeCount);
        Assert.Equal(1, bundle.BlockingViolationEdgeCount);
        Assert.Equal(1, bundle.UnknownPolicyEdgeCount);
        Assert.Equal(1, bundle.WaivedEdgeCount);
    }

    [Fact]
    public void RejectInvalidSourcePolicyCursorAndUnsafeBudgets()
    {
        var graph = MakeGraph(3);
        var key = Assert.Single(AggregatedBoundaryPlanner.Plan(graph, "inside").Bundles).Key;
        Assert.Throws<ArgumentException>(() =>
            AggregatedBoundaryPlanner.Plan(graph, "inside",
                [Finding(graph.Edges[0]) with { Evidence = new SourceEvidence(
                    "other", "wrong", 1, 1, EvidenceKind.Static) }]));
        Assert.Throws<ArgumentException>(() =>
            AggregatedBoundaryPlanner.WitnessesPage(graph, "inside", key, afterEdgeId: "absent"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AggregatedBoundaryPlanner.WitnessesPage(graph, "inside", key, pageSize: 101));
        Assert.Throws<InvalidOperationException>(() =>
            AggregatedBoundaryPlanner.Plan(graph, "inside", maxScanEdges: 2));
        Assert.Throws<KeyNotFoundException>(() =>
            AggregatedBoundaryPlanner.Plan(graph, "unknown"));
        var broken = graph with
        {
            Nodes = graph.Nodes.Select(n => n.SymbolId == "root"
                ? n with { ParentSymbolId = "in:0000" } : n).ToArray()
        };
        Assert.Throws<InvalidDataException>(() =>
            AggregatedBoundaryPlanner.Plan(broken, "inside"));
    }

    [Fact]
    public void ActualGuardianEvaluationSurfacesBlockingFindingInSourceBundle()
    {
        var e = new SourceEvidence("r1", "Service.cs", 8, 4, EvidenceKind.Static);
        var graph = new GraphSnapshot("Demo", "r1",
        [
            new("solution", GraphNodeKind.Solution, "Demo", null, e),
            new("project:Inside", GraphNodeKind.Project, "Inside", "solution", e),
            new("project:Outside", GraphNodeKind.Project, "Outside", "solution", e),
            new("Inside::M:Caller", GraphNodeKind.Method, "Caller", "project:Inside", e),
            new("Outside::M:Callee", GraphNodeKind.Method, "Callee", "project:Outside", e)
        ],
        [new("call:1", "Inside::M:Caller", "Outside::M:Callee", "Calls", e)], []);
        var findings = ArchitectureGuardian.Evaluate(graph,
            [new ForbiddenDependencyRule("NO-CROSS-CALL", "v1", "Inside", "Outside",
                "Calls", RuleSeverity.Blocker)]);
        Assert.Equal(RuleOutcome.Violation, Assert.Single(findings).Outcome);
        var bundle = Assert.Single(
            AggregatedBoundaryPlanner.Plan(graph, "project:Inside", findings).Bundles);
        Assert.Equal(1, bundle.SourceEdgeCount);
        Assert.Equal(1, bundle.ViolationEdgeCount);
        Assert.Equal(1, bundle.BlockingViolationEdgeCount);
        Assert.Equal("call:1", Assert.Single(bundle.PreviewWitnessEdgeIds));
        var unevaluated = ArchitectureGuardian.Evaluate(graph,
            [new ForbiddenDependencyRule("MISSING", "v1", "Inside", "Unknown",
                "Calls", RuleSeverity.Blocker)]);
        var unresolved = AggregatedBoundaryPlanner.Plan(graph, "project:Inside", unevaluated);
        Assert.Equal(1, unresolved.UnscopedPolicyFindingsCount);
        Assert.Equal(1, unresolved.UnscopedBlockerFindingsCount);
        Assert.Equal(0, Assert.Single(unresolved.Bundles).BlockingViolationEdgeCount);
    }

    [Fact]
    public void RejectMoreThan500DistinctBoundaryGroupsInsteadOfSilentlyDroppingThem()
    {
        var graph = MakeGraph(501);
        graph = graph with
        {
            Edges = graph.Edges.Select((edge, i) =>
                edge with { Relation = $"Relation:{i:D4}" }).ToArray()
        };
        Assert.Throws<InvalidOperationException>(() =>
            AggregatedBoundaryPlanner.Plan(graph, "inside"));
    }

    [Fact]
    public void RevisionAndGroupIdentityPreventCrossScopeWitnessReuse()
    {
        var graph = MakeGraph(3);
        var first = Assert.Single(AggregatedBoundaryPlanner.Plan(graph, "inside").Bundles);
        var page = AggregatedBoundaryPlanner.WitnessesPage(graph, "inside", first.Key, 1);
        Assert.True(page.HasMore);
        Assert.Throws<KeyNotFoundException>(() =>
            AggregatedBoundaryPlanner.WitnessesPage(graph, "outside",
                first.Key, 1, page.NextAfterEdgeId));
        var revised = MakeGraph(2, "r2");
        Assert.Throws<ArgumentException>(() =>
            AggregatedBoundaryPlanner.WitnessesPage(revised, "inside",
                first.Key, 1, "call:0002")); // cursor no longer exists in this revision
    }
}
