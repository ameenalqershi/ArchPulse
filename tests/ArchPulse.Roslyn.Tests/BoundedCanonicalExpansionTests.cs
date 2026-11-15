// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class BoundedCanonicalExpansionTests
{
    private static GraphSnapshot Graph()
    {
        var e = new SourceEvidence("r1", "Source.cs", 1, 2, EvidenceKind.Static);
        return new("project", "r1",
        [
            new("c", GraphNodeKind.Method, "C", "a", e),
            new("a", GraphNodeKind.Class, "A", "root", e),
            new("b", GraphNodeKind.Class, "B", "root", e),
            new("root", GraphNodeKind.Solution, "Root", null, e)
        ], [], []);
    }

    [Fact]
    public void ExactRootAndDepthReturnOnlyCanonicalHierarchy()
    {
        var g = Graph();
        var d = LocalViewScopeDescriptor.Create(g, "root", depth: 1);
        var result = BoundedCanonicalExpansion.Execute(g, d);
        Assert.Equal(new[] { "root", "a", "b" }, result.Nodes.Select(x => x.SymbolId));
        Assert.Equal(new[] { "a", "b" }, result.CanonicalLinks.Select(x => x.ChildSymbolId));
        Assert.False(result.Partial);
        Assert.Equal("AuthorizationNotEvaluated", result.AccessStatus);
    }

    [Fact]
    public void NodeBudgetStopsExpansionAndReportsPartial()
    {
        var g = Graph();
        var d = LocalViewScopeDescriptor.Create(g, "root", maxNodes: 2, depth: 3);
        var result = BoundedCanonicalExpansion.Execute(g, d);
        Assert.Equal(new[] { "root", "a" }, result.Nodes.Select(x => x.SymbolId));
        Assert.True(result.Partial);
        Assert.True(result.NodeBudgetReached);
        Assert.Equal("NoContinuationAvailable", result.ContinuationStatus);
    }

    [Fact]
    public void ParentLinksRespectEdgeBudget()
    {
        var g = Graph();
        var d = LocalViewScopeDescriptor.Create(g, "root", maxEdges: 1, depth: 3);
        var result = BoundedCanonicalExpansion.Execute(g, d);
        Assert.Equal(4, result.Nodes.Count);
        Assert.Single(result.CanonicalLinks);
        Assert.True(result.EdgeBudgetReached);
        Assert.True(result.Partial);
    }

    [Fact]
    public void RejectsCrossRevisionAndForgedSelection()
    {
        var g = Graph();
        var descriptor = LocalViewScopeDescriptor.Create(g, "root");
        var newRevision = g with
        {
            Revision = "r2",
            Nodes = g.Nodes.Select(n => n with
            {
                Evidence = n.Evidence with { Revision = "r2" }
            }).ToArray()
        };
        Assert.Throws<ArgumentException>(() =>
            BoundedCanonicalExpansion.Execute(newRevision, descriptor));
        Assert.Throws<ArgumentException>(() =>
            BoundedCanonicalExpansion.Execute(g, descriptor with { AccessStatus = "Authorized" }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BoundedCanonicalExpansion.Execute(g, descriptor with { MaxNodes = 10_000 }));
    }
}
