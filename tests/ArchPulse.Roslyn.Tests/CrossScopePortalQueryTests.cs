// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class CrossScopePortalQueryTests
{
    private static GraphSnapshot ThreeModules(string revision = "r1")
    {
        var staticEvidence = new SourceEvidence(revision, "src/Flow.cs", 4, 3, EvidenceKind.Static);
        var inferred = staticEvidence with { Kind = EvidenceKind.Inferred };
        var observed = staticEvidence with { Kind = EvidenceKind.Observed };
        return new GraphSnapshot("Flow", revision,
        [
            new("root", GraphNodeKind.Solution, "System", null, staticEvidence),
            new("a", GraphNodeKind.Project, "Accounting", "root", staticEvidence),
            new("a1", GraphNodeKind.Method, "CreateInvoice", "a", staticEvidence),
            new("a2", GraphNodeKind.Method, "Validate", "a", inferred),
            new("b", GraphNodeKind.Project, "Inventory", "root", staticEvidence),
            new("b1", GraphNodeKind.Method, "ReserveStock", "b", staticEvidence),
            new("c", GraphNodeKind.Project, "Payments", "root", staticEvidence),
            new("c1", GraphNodeKind.Method, "Charge", "c", staticEvidence)
        ],
        [
            new("000", "a1", "a2", "Calls", staticEvidence),  // inside A -> inside A
            new("001", "a1", "b1", "Calls", staticEvidence),  // outbound A
            new("002", "b1", "c1", "Calls", observed),       // outside A
            new("003", "c1", "a1", "DependsOn", inferred),  // inbound A
            new("004", "a2", "c1", "Calls", observed),       // outbound A
            new("005", "b1", "a2", "DependsOn", staticEvidence)
        ], []);
    }

    [Fact]
    public void CrossingThreeModulesCreatesPortalsWithoutInventingOwnershipOrFeatureMembership()
    {
        var graph = ThreeModules();
        var projection = CrossScopePortalQuery.GetPage(graph, "a", pageSize: 20);
        Assert.Equal(CrossScopePortalPage.ContractSchema, projection.Schema);
        Assert.Equal("Flow", projection.Project);
        Assert.Equal("r1", projection.Revision);
        Assert.Equal("a", projection.ScopeSymbolId);
        Assert.Equal("AuthorizationNotEvaluated", projection.AccessStatus);
        Assert.Equal("NotIndexed", projection.FeatureMembershipStatus);
        Assert.Equal(4, projection.TotalCrossScopeEdges);
        Assert.Equal(2, projection.OutboundCount);
        Assert.Equal(2, projection.InboundCount);
        Assert.Equal(new[] { "001", "003", "004", "005" },
            projection.Portals.Select(x => x.EdgeId));
        Assert.Equal(new[] { "Outbound", "Inbound", "Outbound", "Inbound" },
            projection.Portals.Select(x => x.Direction));
        Assert.All(projection.Portals, x =>
        {
            Assert.Equal(graph.Edges.Single(e => e.EdgeId == x.EdgeId), x.Edge);
            Assert.Equal("r1", x.ExternalNode.Evidence.Revision);
            Assert.DoesNotContain(x.ExternalNode.SymbolId, new[] { "a", "a1", "a2" });
            Assert.Contains(x.InternalNode.SymbolId, new[] { "a", "a1", "a2" });
        });
        Assert.Equal(2, projection.EvidenceCounts.Single(x => x.Kind == EvidenceKind.Static).Count);
        Assert.Equal(1, projection.EvidenceCounts.Single(x => x.Kind == EvidenceKind.Inferred).Count);
        Assert.Equal(1, projection.EvidenceCounts.Single(x => x.Kind == EvidenceKind.Observed).Count);
        Assert.Equal(0, projection.EvidenceCounts.Single(x => x.Kind == EvidenceKind.Verified).Count);
        Assert.Equal("b1", projection.Portals[0].ExternalNode.SymbolId);
        Assert.Equal("c1", projection.Portals[1].ExternalNode.SymbolId);
        Assert.Equal("Calls", projection.Portals[0].Relation);
        Assert.Equal("DependsOn", projection.Portals[1].Relation);

        // The exact canonical parent remains unchanged for all three modules.
        Assert.Equal("a", graph.Nodes.Single(n => n.SymbolId == "a1").ParentSymbolId);
        Assert.Equal("b", graph.Nodes.Single(n => n.SymbolId == "b1").ParentSymbolId);
        Assert.Equal("c", graph.Nodes.Single(n => n.SymbolId == "c1").ParentSymbolId);
    }

    [Fact]
    public void CursorPaginationIsStableAndCannotCrossScopeOrRevision()
    {
        var graph = ThreeModules();
        var one = CrossScopePortalQuery.GetPage(graph, "a", pageSize: 2);
        Assert.Equal(new[] { "001", "003" }, one.Portals.Select(x => x.EdgeId));
        Assert.True(one.HasMore);
        Assert.Equal("003", one.NextAfterEdgeId);
        var two = CrossScopePortalQuery.GetPage(graph, "a", pageSize: 2,
            afterEdgeId: one.NextAfterEdgeId);
        Assert.Equal(new[] { "004", "005" }, two.Portals.Select(x => x.EdgeId));
        Assert.False(two.HasMore);
        Assert.Null(two.NextAfterEdgeId);
        Assert.Throws<ArgumentException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", afterEdgeId: "000")); // internal edge
        Assert.Throws<ArgumentException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", afterEdgeId: "002")); // external edge
        Assert.Throws<ArgumentException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", afterEdgeId: "unrecognized"));
        Assert.Throws<ArgumentException>(() =>
            CrossScopePortalQuery.GetPage(graph, "root", afterEdgeId: "001"));
        var revised = ThreeModules("r2") with { Edges = [] };
        Assert.Throws<ArgumentException>(() =>
            CrossScopePortalQuery.GetPage(revised, "a", afterEdgeId: "003"));
    }

    [Fact]
    public void ScopeRootAndLeafContainmentAreExactRatherThanEdgeBased()
    {
        var graph = ThreeModules();
        var root = CrossScopePortalQuery.GetPage(graph, "root");
        Assert.Empty(root.Portals);
        Assert.Equal(0, root.TotalCrossScopeEdges);
        var leaf = CrossScopePortalQuery.GetPage(graph, "a1");
        Assert.Equal(new[] { "000", "001", "003" }, leaf.Portals.Select(x => x.EdgeId));
        Assert.Equal(new[] { "Outbound", "Outbound", "Inbound" },
            leaf.Portals.Select(x => x.Direction));
        Assert.Equal(3, leaf.TotalCrossScopeEdges);
        Assert.Equal("a2", leaf.Portals[0].ExternalNode.SymbolId);
    }

    [Fact]
    public void RejectsUnsafeBoundsUnknownScopeAndCorruptOwnership()
    {
        var graph = ThreeModules();
        Assert.Throws<ArgumentException>(() => CrossScopePortalQuery.GetPage(graph, ""));
        Assert.Throws<ArgumentException>(() =>
            CrossScopePortalQuery.GetPage(graph, new string('x', 2049)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", pageSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", pageSize: 101));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", maxScanEdges: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", maxScanEdges: 1_000_001));
        Assert.Throws<InvalidOperationException>(() =>
            CrossScopePortalQuery.GetPage(graph, "a", maxScanEdges: 5));
        Assert.Throws<KeyNotFoundException>(() =>
            CrossScopePortalQuery.GetPage(graph, "missing"));
        var corrupt = graph with
        {
            Nodes = graph.Nodes.Select(n =>
            n.SymbolId == "root" ? n with { ParentSymbolId = "a1" } : n).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => CrossScopePortalQuery.GetPage(corrupt, "a"));
    }

    [Fact]
    public void WideGraphIsPagedWithoutAllocatingPortalsForEveryCrossing()
    {
        var evidence = new SourceEvidence("wide", "Wide.cs", 1, 1, EvidenceKind.Static);
        var nodes = Enumerable.Range(0, 12_000).Select(i =>
            new GraphNode($"node:{i:D5}", GraphNodeKind.Method, "Method",
                i % 2 == 0 ? "inside" : "outside", evidence))
            .Concat([
                new GraphNode("inside", GraphNodeKind.Project, "Inside", null, evidence),
                new GraphNode("outside", GraphNodeKind.Project, "Outside", null, evidence)
            ]).ToArray();
        var edges = Enumerable.Range(0, 6_000).Select(i =>
            new GraphEdge($"edge:{i:D5}", $"node:{i * 2:D5}",
                $"node:{i * 2 + 1:D5}", "Calls", evidence)).Reverse().ToArray();
        var graph = new GraphSnapshot("Wide", "wide", nodes, edges, []);
        var first = CrossScopePortalQuery.GetPage(graph, "inside", pageSize: 100);
        Assert.Equal(6_000, first.TotalCrossScopeEdges);
        Assert.Equal(100, first.Portals.Count);
        Assert.Equal("edge:00099", first.NextAfterEdgeId);
        Assert.True(first.HasMore);
        var next = CrossScopePortalQuery.GetPage(graph, "inside", pageSize: 100,
            afterEdgeId: first.NextAfterEdgeId);
        Assert.Equal("edge:00100", next.Portals[0].EdgeId);
        Assert.Equal("edge:00199", next.Portals[^1].EdgeId);
        Assert.Equal(6_000, next.TotalCrossScopeEdges);
    }
}
