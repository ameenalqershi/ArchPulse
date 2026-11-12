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
        var tampered = graph with { Nodes = graph.Nodes.Select(n =>
            n with { Evidence = n.Evidence with { Revision = "rev2" } }).ToArray() };
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
