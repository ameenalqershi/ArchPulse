// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class GraphSnapshotStoreTests
{
    private static GraphSnapshot MakeGraph(string source, string revision) =>
        StaticGraphIndexer.IndexSources("Invoices",
            new Dictionary<string, string> { ["Invoices.cs"] = source }, revision);

    [Fact]
    public void SavesAndLoadsSameSnapshotAndRepeatWriteIsIdempotent()
    {
        using var temp = new TempDirectory();
        var graph = MakeGraph("public class Invoice { public void Pay() {} }", "abc");
        var store = new GraphSnapshotStore(temp.Path);
        string file = store.Save(graph);
        Assert.Equal(file, store.Save(graph));
        var loaded = Assert.IsType<GraphSnapshot>(store.Load("Invoices", "abc"));
        Assert.Equal(graph.Nodes.Select(n => n.SymbolId), loaded.Nodes.Select(n => n.SymbolId));
        Assert.Equal("abc", loaded.Revision);
        Assert.Null(store.Load("Invoices", "different-revision"));
        Assert.Single(Directory.GetFiles(temp.Path, "*.graph.json"));
    }

    [Fact]
    public void RejectsChangingImmutableSnapshotInSameRevision()
    {
        using var temp = new TempDirectory();
        var store = new GraphSnapshotStore(temp.Path);
        store.Save(MakeGraph("public class A {}", "rev"));
        Assert.Throws<InvalidOperationException>(() =>
            store.Save(MakeGraph("public class B {}", "rev")));
    }

    [Fact]
    public void RejectsUnprovenOrphanEdge()
    {
        var graph = MakeGraph("public class A {}", "rev");
        var evidence = new SourceEvidence("rev", "A.cs", 1, 1, EvidenceKind.Inferred);
        var modified = graph with
        {
            Edges = new[] { new GraphEdge("fake", "project:Invoices", "method:absent", "Calls", evidence) }
        };
        Assert.Throws<InvalidDataException>(() => GraphSnapshotStore.Validate(modified));
    }

    [Fact]
    public void CanonicalOwnershipCyclesAreRejected()
    {
        var graph = MakeGraph("public class A {}", "rev");
        var type = Assert.Single(graph.Nodes, n => n.Kind == GraphNodeKind.Class);
        var parent = Assert.Single(graph.Nodes, n => n.SymbolId == type.ParentSymbolId);
        var nodes = graph.Nodes.Select(n => n.SymbolId == parent.SymbolId
            ? n with { ParentSymbolId = type.SymbolId } : n).ToArray();
        Assert.Throws<InvalidDataException>(() => GraphSnapshotStore.Validate(graph with { Nodes = nodes }));
    }

    [Fact]
    public void DiffTracksAddedNodesAcrossRevisions()
    {
        var earlier = MakeGraph("public class A { public void One() {} }", "v1");
        var later = MakeGraph("public class A { public void One() {} public void Two() {} }", "v2");
        var delta = GraphSnapshotStore.Compare(earlier, later);
        Assert.Single(delta.AddedNodes);
        Assert.Contains("Two", delta.AddedNodes[0], StringComparison.Ordinal);
        Assert.Empty(delta.RemovedNodes);
    }

    [Fact]
    public void DiffDoesNotReportEveryRetainedSymbolOnlyBecauseSourceRevisionChanges()
    {
        var previous = MakeGraph("public class A { public void One() {} }", "rev1");
        var current = MakeGraph("public class A { public void One() {} }", "rev2");
        var diff = GraphSnapshotStore.Compare(previous, current);
        Assert.Empty(diff.AddedNodes);
        Assert.Empty(diff.RemovedNodes);
        Assert.Empty(diff.AddedEdges);
        Assert.Empty(diff.RemovedEdges);
        Assert.Empty(diff.ChangedNodes);
        Assert.Empty(diff.ChangedEdges);
    }

    [Fact]
    public void DiffTracksRetainedNodeLabelAndLocationWithoutConfusingAdditionWithChange()
    {
        var previous = MakeGraph("public class A { public void One() {} }", "rev1");
        var current = MakeGraph("public class A { public void One() {} }", "rev2");
        var method = Assert.Single(current.Nodes, node => node.Kind == GraphNodeKind.Method);
        var updated = current with
        {
            Nodes = current.Nodes.Select(node => node.SymbolId == method.SymbolId
                ? node with { Label = "One() revised", Evidence = node.Evidence with { Line = node.Evidence.Line + 1 } }
                : node).ToArray()
        };

        var diff = GraphSnapshotStore.Compare(previous, updated);
        Assert.Equal(method.SymbolId, Assert.Single(diff.ChangedNodes));
        Assert.Empty(diff.AddedNodes);
        Assert.Empty(diff.RemovedNodes);
    }

    [Fact]
    public void DiffTracksRetainedEdgeRelationAndEvidenceChanges()
    {
        var previous = MakeGraph("""
            public class A
            {
                public int One() => Two();
                public int Two() => 1;
            }
            """, "rev1");
        var current = MakeGraph("""
            public class A
            {
                public int One() => Two();
                public int Two() => 1;
            }
            """, "rev2");
        var edge = Assert.Single(current.Edges);
        var changed = current with
        {
            Edges = [edge with
            {
                Relation = "DependsOn",
                Evidence = edge.Evidence with { RelativeFile = "Changed.cs" }
            }]
        };

        var diff = GraphSnapshotStore.Compare(previous, changed);
        Assert.Equal(edge.EdgeId, Assert.Single(diff.ChangedEdges));
        Assert.Empty(diff.AddedEdges);
        Assert.Empty(diff.RemovedEdges);
    }

    [Fact]
    public void DiffTracksChangedOwnershipForSameExactSymbolId()
    {
        var evidenceBefore = new SourceEvidence("rev1", "X.cs", 1, 1, EvidenceKind.Static);
        var evidenceAfter = evidenceBefore with { Revision = "rev2" };
        var previous = new GraphSnapshot("X", "rev1",
        [
            new GraphNode("root1", GraphNodeKind.Solution, "One", null, evidenceBefore),
            new GraphNode("root2", GraphNodeKind.Solution, "Two", null, evidenceBefore),
            new GraphNode("child", GraphNodeKind.Method, "Child", "root1", evidenceBefore)
        ], [], []);
        var current = new GraphSnapshot("X", "rev2",
        [
            new GraphNode("root1", GraphNodeKind.Solution, "One", null, evidenceAfter),
            new GraphNode("root2", GraphNodeKind.Solution, "Two", null, evidenceAfter),
            new GraphNode("child", GraphNodeKind.Method, "Child", "root2", evidenceAfter)
        ], [], []);
        var diff = GraphSnapshotStore.Compare(previous, current);
        Assert.Equal("child", Assert.Single(diff.ChangedNodes));
    }

    [Fact]
    public void BodyOnlyEditWithUnchangedGraphStructureDoesNotProveNoSourceChange()
    {
        var previous = MakeGraph("public class A { public int One() => 1; }", "rev1");
        var current = MakeGraph("public class A { public int One() => 2; }", "rev2");
        var diff = GraphSnapshotStore.Compare(previous, current);
        Assert.Empty(diff.ChangedNodes);
        Assert.Empty(diff.ChangedEdges);
        // This intentionally scoped projection cannot detect a body-only edit.
    }

    [Fact]
    public void GraphQueriesOnlyFollowEvidenceBackedRelations()
    {
        var graph = MakeGraph("""
            public class A
            {
                public int One() => Two();
                public int Two() => Three();
                public int Three() => 3;
            }
            """, "v1");
        var method1 = Assert.Single(graph.Nodes, n => n.Kind == GraphNodeKind.Method && n.Label.Contains("One("));
        var method3 = Assert.Single(graph.Nodes, n => n.Kind == GraphNodeKind.Method && n.Label.Contains("Three("));
        Assert.Equal(2, GraphQueries.ShortestStaticPath(graph, method1.SymbolId, method3.SymbolId).Count);
        Assert.Single(GraphQueries.Neighbors(graph, method1.SymbolId));
        Assert.Empty(GraphQueries.ShortestStaticPath(graph, method3.SymbolId, method1.SymbolId));
    }

    [Fact]
    public void InvalidEvidenceRevisionIsRejected()
    {
        var graph = MakeGraph("public class A {}", "rev1");
        var tampered = graph with
        {
            Nodes = graph.Nodes.Select(n =>
            n with { Evidence = n.Evidence with { Revision = "rev2" } }).ToArray()
        };
        Assert.Throws<InvalidDataException>(() => GraphSnapshotStore.Validate(tampered));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-graph-test-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
