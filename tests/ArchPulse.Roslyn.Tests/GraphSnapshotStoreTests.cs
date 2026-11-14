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
    public void CorruptSnapshotJsonRetainsOriginalJsonExceptionForNonHttpConsumers()
    {
        using var temp = new TempDirectory();
        var store = new GraphSnapshotStore(temp.Path);
        var file = store.Save(MakeGraph("public class A {}", "rev1"));
        File.WriteAllText(file, "{private-diagnostic-marker");
        Assert.Throws<System.Text.Json.JsonException>(() => store.Load("Invoices", "rev1"));
    }

    [Fact]
    public void MismatchedPersistedIdentityRetainsInvalidDataExceptionAndMissingKeysStayNull()
    {
        using var temp = new TempDirectory();
        var store = new GraphSnapshotStore(temp.Path);
        var file = store.Save(MakeGraph("public class A {}", "rev1"));
        var payload = File.ReadAllText(file);
        Assert.Contains("\"Project\": \"Invoices\"", payload, StringComparison.Ordinal);
        File.WriteAllText(file, payload.Replace("\"Project\": \"Invoices\"",
            "\"Project\": \"Forged\"", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => store.Load("Invoices", "rev1"));
        Assert.Null(store.Load("Invoices", "unindexed"));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("node")]
    [InlineData("evidence")]
    [InlineData("edge")]
    public void RejectsDuplicateJsonKeysBeforeAcceptingPersistedSourceEvidence(string location)
    {
        using var temp = new TempDirectory();
        var store = new GraphSnapshotStore(temp.Path);
        var file = store.Save(MakeGraph(
            "public class A { public int One() => Two(); public int Two() => 2; }", "rev1"));
        var original = File.ReadAllText(file);
        var (target, duplicate) = location switch
        {
            "root" => ("\"Project\": \"Invoices\"", "\"Project\": \"spoofed\", \"Project\": \"Invoices\""),
            "node" => ("\"Label\":", "\"Label\": \"spoofed\", \"Label\":"),
            "evidence" => ("\"Evidence\": {", "\"Evidence\": { \"Revision\": \"spoofed\","),
            "edge" => ("\"Relation\": \"Calls\"", "\"Relation\": \"DependsOn\", \"Relation\": \"Calls\""),
            _ => throw new ArgumentOutOfRangeException(nameof(location))
        };
        Assert.Contains(target, original, StringComparison.Ordinal);
        File.WriteAllText(file, original.Replace(target, duplicate, StringComparison.Ordinal));

        // Last-value-wins deserialization used to silently accept this
        // ambiguous evidence while resolving the original values.
        Assert.Throws<InvalidDataException>(() => store.Load("Invoices", "rev1"));
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
    public void ValidatesThirtyThousandDeepOwnershipNodesWithoutRecursiveTraversal()
    {
        const int count = 30_000;
        var evidence = new SourceEvidence("deep-revision", "Demo.cs", 1, 1, EvidenceKind.Static);
        var nodes = Enumerable.Range(0, count)
            .Select(index => new GraphNode($"node:{index}", GraphNodeKind.Class,
                $"Node {index}", index == 0 ? null : $"node:{index - 1}", evidence))
            .Reverse().ToArray();

        var graph = new GraphSnapshot("Deep", "deep-revision", nodes, [], []);
        GraphSnapshotStore.Validate(graph);
        Assert.Equal(count, graph.Nodes.Count);
    }

    [Fact]
    public void RejectsDeepCanonicalCycleEvenIfNodesAreInReverseOrder()
    {
        const int count = 18_000;
        var evidence = new SourceEvidence("cycle-revision", "Demo.cs", 1, 1, EvidenceKind.Static);
        var nodes = Enumerable.Range(0, count).Select(index =>
            new GraphNode($"node:{index}", GraphNodeKind.Class, $"Node {index}",
                index == 0 ? $"node:{count - 1}" : $"node:{index - 1}", evidence))
            .Reverse().ToArray();

        var graph = new GraphSnapshot("Cyclic", "cycle-revision", nodes, [], []);
        var ex = Assert.Throws<InvalidDataException>(() => GraphSnapshotStore.Validate(graph));
        Assert.Contains("Cycle in canonical ownership hierarchy", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsManyIndependentRootsAndSharedAncestors()
    {
        var evidence = new SourceEvidence("roots", "Demo.cs", 1, 1, EvidenceKind.Static);
        var nodes = Enumerable.Range(0, 10_000)
            .Select(index => new GraphNode($"node:{index}", GraphNodeKind.Class, $"Node {index}",
                index < 100 ? null : $"node:{index % 100}", evidence))
            .ToArray();

        GraphSnapshotStore.Validate(new GraphSnapshot("Roots", "roots", nodes, [], []));
    }

    [Fact]
    public void NeighborPagesRemainOrderedBoundedAndCompleteOnReversedEdges()
    {
        var evidence = new SourceEvidence("page-rev", "Page.cs", 1, 1, EvidenceKind.Static);
        var nodes = Enumerable.Range(0, 11)
            .Select(index => new GraphNode($"N{index}", GraphNodeKind.Class,
                $"Node {index}", null, evidence)).ToArray();
        var edges = Enumerable.Range(1, 9)
            .Select(index => new GraphEdge($"edge-{index:000}", "N0", $"N{index}",
                "Calls", evidence))
            .Append(new GraphEdge("unrelated", "N9", "N10", "Calls", evidence))
            .Reverse().ToArray();
        var graph = new GraphSnapshot("Page", "page-rev", nodes, edges, []);
        string? after = null;
        var received = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var page = GraphQueries.NeighborsPage(graph, "N0", 2, after);
            Assert.Equal(GraphNeighborPage.ContractSchema, page.Schema);
            Assert.Equal("page-rev", page.Revision);
            Assert.Equal("N0", page.SymbolId);
            Assert.InRange(page.Edges.Count, 1, 2);
            received.AddRange(page.Edges.Select(edge => edge.EdgeId));
            if (!page.HasMore)
            {
                Assert.Null(page.NextAfterEdgeId);
                break;
            }
            Assert.Equal(page.Edges[^1].EdgeId, page.NextAfterEdgeId);
            after = page.NextAfterEdgeId;
        }
        Assert.Equal(Enumerable.Range(1, 9).Select(i => $"edge-{i:000}"), received);
    }

    [Fact]
    public void NeighborPageRejectsForgedOrNonIncidentCursorsAndUnsafeLimits()
    {
        var evidence = new SourceEvidence("page-rev", "Page.cs", 1, 1, EvidenceKind.Static);
        var graph = new GraphSnapshot("Page", "page-rev",
        [
            new GraphNode("A", GraphNodeKind.Class, "A", null, evidence),
            new GraphNode("B", GraphNodeKind.Class, "B", null, evidence),
            new GraphNode("C", GraphNodeKind.Class, "C", null, evidence)
        ],
        [
            new GraphEdge("ab", "A", "B", "Calls", evidence),
            new GraphEdge("bc", "B", "C", "Calls", evidence)
        ], []);
        Assert.Throws<ArgumentOutOfRangeException>(() => GraphQueries.NeighborsPage(graph, "A", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GraphQueries.NeighborsPage(graph, "A", 101));
        Assert.Throws<ArgumentException>(() => GraphQueries.NeighborsPage(graph, "A", 1, "bc"));
        Assert.Throws<ArgumentException>(() => GraphQueries.NeighborsPage(graph, "A", 1, "unknown"));
        Assert.Throws<KeyNotFoundException>(() => GraphQueries.NeighborsPage(graph, "unknown"));
        var last = GraphQueries.NeighborsPage(graph, "A", 1, "ab");
        Assert.Empty(last.Edges);
        Assert.False(last.HasMore);
        Assert.Null(last.NextAfterEdgeId);
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
