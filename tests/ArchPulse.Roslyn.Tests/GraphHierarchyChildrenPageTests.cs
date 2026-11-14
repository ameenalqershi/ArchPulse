// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class GraphHierarchyChildrenPageTests
{
    private static GraphSnapshot Fixture(int count = 11)
    {
        var evidence = new SourceEvidence("sha-1", "Folder/Source.cs", 4, 3, EvidenceKind.Static);
        var roots = new[]
        {
            new GraphNode("root", GraphNodeKind.Solution, "Root", null, evidence),
            new GraphNode("other-root", GraphNodeKind.Solution, "Other root", null,
                evidence with { Kind = EvidenceKind.Inferred })
        };
        var children = Enumerable.Range(0, count)
            .Select(i => new GraphNode($"node:{i:D5}", GraphNodeKind.Class,
                $"Child {i}", "root", evidence))
            .Reverse();
        var grandchild = new GraphNode("grandchild", GraphNodeKind.Method,
            "Method", "node:00000", evidence);
        return new GraphSnapshot("Hierarchy", "sha-1",
            roots.Concat(children).Append(grandchild).ToArray(), [], []);
    }

    [Fact]
    public void RootPageReturnsOnlyCanonicalRootsWithOriginalEvidenceKinds()
    {
        var page = GraphQueries.ChildrenPage(Fixture());
        Assert.Equal(GraphChildPage.ContractSchema, page.Schema);
        Assert.Equal("Hierarchy", page.Project);
        Assert.Equal("sha-1", page.Revision);
        Assert.Null(page.ParentSymbolId);
        Assert.Equal(new[] { "other-root", "root" },
            page.Nodes.Select(n => n.SymbolId));
        Assert.Equal(EvidenceKind.Inferred, page.Nodes[0].Evidence.Kind);
        Assert.False(page.HasMore);
        Assert.Null(page.NextAfterSymbolId);
    }

    [Fact]
    public void PagesChildrenInStableOrdinalOrderWithoutReturningGrandchildren()
    {
        var graph = Fixture();
        var cursor = (string?)null;
        var received = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var page = GraphQueries.ChildrenPage(graph, "root", 3, cursor);
            Assert.Equal("root", page.ParentSymbolId);
            Assert.InRange(page.Nodes.Count, 0, 3);
            Assert.All(page.Nodes, n => Assert.Equal("root", n.ParentSymbolId));
            Assert.All(page.Nodes, n => Assert.Equal("sha-1", n.Evidence.Revision));
            received.AddRange(page.Nodes.Select(n => n.SymbolId));
            if (!page.HasMore)
            {
                Assert.Null(page.NextAfterSymbolId);
                break;
            }
            Assert.Equal(page.Nodes[^1].SymbolId, page.NextAfterSymbolId);
            cursor = page.NextAfterSymbolId;
        }
        Assert.Equal(Enumerable.Range(0, 11).Select(i => $"node:{i:D5}"), received);
        Assert.DoesNotContain("grandchild", received);
        var exhausted = GraphQueries.ChildrenPage(graph, "root", 2, "node:00010");
        Assert.Empty(exhausted.Nodes);
        Assert.False(exhausted.HasMore);
    }

    [Fact]
    public void RejectsMissingParentNonChildCursorAndUnsafeLimits()
    {
        var graph = Fixture();
        Assert.Throws<KeyNotFoundException>(() =>
            GraphQueries.ChildrenPage(graph, "missing"));
        Assert.Throws<ArgumentException>(() =>
            GraphQueries.ChildrenPage(graph, "", 1));
        Assert.Throws<ArgumentException>(() =>
            GraphQueries.ChildrenPage(graph, "root", 2, "other-root"));
        Assert.Throws<ArgumentException>(() =>
            GraphQueries.ChildrenPage(graph, "root", 2, "grandchild"));
        Assert.Throws<ArgumentException>(() =>
            GraphQueries.ChildrenPage(graph, null, 2, "node:00000"));
        Assert.Throws<ArgumentException>(() =>
            GraphQueries.ChildrenPage(graph, "root", 2, "absent"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GraphQueries.ChildrenPage(graph, "root", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GraphQueries.ChildrenPage(graph, "root", 101));
        Assert.Throws<ArgumentException>(() =>
            GraphQueries.ChildrenPage(graph, "root", 1, new string('a', 2049)));
        Assert.Throws<ArgumentException>(() =>
            GraphQueries.ChildrenPage(graph, new string('a', 2049)));
    }

    [Fact]
    public void WideHierarchyRetainsAtMostPageSizeItemsAndCanReadNextPage()
    {
        var graph = Fixture(count: 20_000);
        var first = GraphQueries.ChildrenPage(graph, "root", 25);
        Assert.Equal(25, first.Nodes.Count);
        Assert.True(first.HasMore);
        Assert.Equal("node:00024", first.NextAfterSymbolId);

        var next = GraphQueries.ChildrenPage(graph, "root", 25, first.NextAfterSymbolId);
        Assert.Equal("node:00025", next.Nodes[0].SymbolId);
        Assert.Equal("node:00049", next.Nodes[^1].SymbolId);
        Assert.True(next.HasMore);
    }

    [Fact]
    public void LeafNodeReturnsEmptyPageRatherThanInventingChildRelations()
    {
        var page = GraphQueries.ChildrenPage(Fixture(), "grandchild", 5);
        Assert.Empty(page.Nodes);
        Assert.False(page.HasMore);
        Assert.Null(page.NextAfterSymbolId);
    }
}
