// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class CanonicalHierarchyTopologyTests
{
    private static GraphSnapshot Graph(string revision = "sha1", string renamed = "Service",
        EvidenceKind childEvidence = EvidenceKind.Inferred)
    {
        var evidence = new SourceEvidence(revision, "src/Service.cs", 2, 3, EvidenceKind.Static);
        var nodes = new GraphNode[]
        {
            new("L6", GraphNodeKind.Method, "Run()", "L5", evidence),
            new("L1", GraphNodeKind.Project, "Project", "L0", evidence),
            new("L0", GraphNodeKind.Solution, "System", null, evidence),
            new("L3", GraphNodeKind.Class, renamed, "L2", evidence with { Kind = childEvidence }),
            new("L2", GraphNodeKind.Namespace, "Namespace", "L1", evidence),
            new("L5", GraphNodeKind.Class, "Implementation", "L4", evidence),
            new("L4", GraphNodeKind.Class, "Component", "L3", evidence),
            new("other", GraphNodeKind.Solution, "Independent root", null, evidence)
        };
        // Cross-scope Calls do not change canonical parent topology.
        return new GraphSnapshot("Demo", revision, nodes,
            [new GraphEdge("cross", "L6", "other", "Calls", evidence)], []);
    }

    [Fact]
    public void SevenLevelHierarchyPreservesExactPathCountsAndSourceEvidence()
    {
        var topology = new CanonicalHierarchyTopology(Graph());
        var scope = topology.Describe("L6");
        Assert.Equal(GraphScopeTopology.ContractSchema, scope.Schema);
        Assert.Equal("Demo", scope.Project);
        Assert.Equal("sha1", scope.Revision);
        Assert.Equal("L0", scope.RootSymbolId);
        Assert.Equal(6, scope.Depth);
        Assert.Equal(new[] { "L0", "L1", "L2", "L3", "L4", "L5", "L6" },
            scope.Breadcrumb.Select(x => x.SymbolId));
        Assert.All(scope.Breadcrumb, n => Assert.Equal("sha1", n.Evidence.Revision));
        Assert.Equal("Run()", scope.Node.Label);
        Assert.Equal(0, scope.DirectChildrenCount);
        Assert.Equal(0, scope.DescendantCount);
        var root = topology.Describe("L0");
        Assert.Equal(6, root.DescendantCount);
        Assert.Equal(1, root.DirectChildrenCount);
        Assert.Equal(0, root.Depth);
        Assert.Equal(0, topology.Describe("other").DescendantCount);
        Assert.Equal(new[] { "L0", "other" }, topology.Roots.Select(n => n.SymbolId));
    }

    [Fact]
    public void StableIdentifiersSurviveLabelRenameAcrossExactSourceRevisions()
    {
        var first = new CanonicalHierarchyTopology(Graph()).Describe("L6");
        var second = new CanonicalHierarchyTopology(
            Graph("sha2", renamed: "RenamedService")).Describe("L6");
        Assert.Equal(first.Breadcrumb.Select(n => n.SymbolId),
            second.Breadcrumb.Select(n => n.SymbolId));
        Assert.Equal("Service", first.Breadcrumb[3].Label);
        Assert.Equal("RenamedService", second.Breadcrumb[3].Label);
        Assert.Equal("sha2", second.Node.Evidence.Revision);
        Assert.Equal(first.RootSymbolId, second.RootSymbolId);
    }

    [Fact]
    public void DirectChildrenEvidenceModesStaySeparateAndDoNotPromoteInferred()
    {
        var scope = new CanonicalHierarchyTopology(Graph()).Describe("L2");
        Assert.Equal(1, scope.DirectChildrenCount);
        Assert.Equal(4, scope.DescendantCount);
        Assert.Equal(1, Assert.Single(scope.DirectChildEvidence,
            e => e.Kind == EvidenceKind.Inferred).Count);
        Assert.Equal(0, Assert.Single(scope.DirectChildEvidence,
            e => e.Kind == EvidenceKind.Verified).Count);
        Assert.Equal(EvidenceKind.Inferred,
            new CanonicalHierarchyTopology(Graph()).Describe("L3").Node.Evidence.Kind);
    }

    [Fact]
    public void IndexedChildrenPaginationValidatesExactScopeAndStableOrdering()
    {
        var topology = new CanonicalHierarchyTopology(Graph());
        Assert.Equal(new[] { "L0" }, topology.ChildrenPage(pageSize: 1).Nodes.Select(n => n.SymbolId));
        var nextRoot = topology.ChildrenPage(pageSize: 1, afterSymbolId: "L0");
        Assert.Equal("other", Assert.Single(nextRoot.Nodes).SymbolId);
        Assert.False(nextRoot.HasMore);
        Assert.Equal("L1", Assert.Single(topology.ChildrenPage("L0").Nodes).SymbolId);
        Assert.Equal("L3", Assert.Single(topology.ChildrenPage("L2").Nodes).SymbolId);
        Assert.Empty(topology.ChildrenPage("L6").Nodes);
        Assert.Throws<KeyNotFoundException>(() => topology.ChildrenPage("missing"));
        Assert.Throws<ArgumentException>(() => topology.ChildrenPage("L2", afterSymbolId: "L1"));
        Assert.Throws<ArgumentException>(() => topology.ChildrenPage(null, afterSymbolId: "L1"));
        Assert.Throws<ArgumentOutOfRangeException>(() => topology.ChildrenPage(pageSize: 101));
        Assert.Throws<ArgumentException>(() => topology.Describe(new string('x', 2049)));
        Assert.Throws<KeyNotFoundException>(() => topology.Describe("missing"));
    }

    [Fact]
    public void LargeWideHierarchyUsesExactCursorAndPreservesAllDescendants()
    {
        var evidence = new SourceEvidence("r1", "File.cs", 0, 0, EvidenceKind.Static);
        var nodes = Enumerable.Range(0, 30_000)
            .Select(i => new GraphNode($"n:{i:D5}", GraphNodeKind.Class, $"Class {i}",
                "root", evidence)).Reverse()
            .Append(new GraphNode("root", GraphNodeKind.Solution, "Root", null, evidence))
            .ToArray();
        var topology = new CanonicalHierarchyTopology(
            new GraphSnapshot("Wide", "r1", nodes, [], []));
        Assert.Equal(30_000, topology.Describe("root").DescendantCount);
        var first = topology.ChildrenPage("root", pageSize: 100);
        Assert.Equal(100, first.Nodes.Count);
        Assert.Equal("n:00099", first.NextAfterSymbolId);
        var next = topology.ChildrenPage("root", pageSize: 100,
            afterSymbolId: first.NextAfterSymbolId);
        Assert.Equal("n:00100", next.Nodes[0].SymbolId);
        Assert.Equal("n:00199", next.Nodes[^1].SymbolId);
    }

    [Fact]
    public void DeepHierarchyDoesNotOverflowStackAndRejectsOversizedBreadcrumb()
    {
        var evidence = new SourceEvidence("deep", "File.cs", 1, 1, EvidenceKind.Static);
        var nodes = Enumerable.Range(0, 30_000)
            .Select(i => new GraphNode($"d:{i}", GraphNodeKind.Class, "Child",
                i == 0 ? null : $"d:{i - 1}", evidence)).Reverse().ToArray();
        var topology = new CanonicalHierarchyTopology(
            new GraphSnapshot("Deep", "deep", nodes, [], []));
        Assert.Equal(29_999, topology.Describe("d:0").DescendantCount);
        Assert.Throws<InvalidOperationException>(() => topology.Describe("d:29999"));
    }

    [Fact]
    public void CorruptParentCyclesFailValidationBeforeAnyProjection()
    {
        var graph = Graph();
        var invalid = graph with
        {
            Nodes = graph.Nodes.Select(n => n.SymbolId == "L0"
                ? n with { ParentSymbolId = "L6" } : n).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => new CanonicalHierarchyTopology(invalid));
    }
}
