// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using ArchPulse.Graph;
using ArchPulse.Guardian;
using Xunit;

namespace ArchPulse.Guardian.Tests;

public class ArchitectureGuardianTests
{
    private const string Sha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly ForbiddenDependencyRule Rule = new(
        "AP-BOUNDARY-001", 1, GraphEdgeKind.Calls,
        "M:Example.Domain.", "M:Example.Infrastructure.", ViolationSeverity.Blocker);
    private static (GraphSnapshot Graph, GraphEdge Edge) Fixture(bool incomplete = false)
    {
        var graph = new GraphBuilder(Sha);
        var location = new SourceSpan("src/Domain/Order.cs", 10, 5, 10, 20);
        graph.AddNode(new GraphNode("n1", GraphNodeKind.Method, "Domain.Create",
            new(Sha, EvidenceKind.Static, "Roslyn", "M:Example.Domain.Order.Create", location)));
        graph.AddNode(new GraphNode("n2", GraphNodeKind.Method, "Infrastructure.Save",
            new(Sha, EvidenceKind.Static, "Roslyn", "M:Example.Infrastructure.Store.Save", location)));
        graph.AddEdge("n1", "n2", GraphEdgeKind.Calls,
            new(Sha, EvidenceKind.Static, "Roslyn", "M:Example.Infrastructure.Store.Save", location));
        if (incomplete) graph.AddDiagnostic("Unresolved syntax-only call");
        var snapshot = graph.Build();
        return (snapshot, Assert.Single(snapshot.Edges));
    }

    [Fact]
    public void ForbiddenCallIsLinkedToSourceAndBlocksCi()
    {
        var (graph, edge) = Fixture();
        var result = ArchitectureGuardian.Evaluate(graph, [Rule]);
        Assert.Equal(ArchitectureVerdict.Violation, result.Verdict);
        Assert.True(result.BlocksCi);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("AP-BOUNDARY-001", finding.RuleId);
        Assert.Equal(edge.Id, finding.EdgeId);
        Assert.Equal("src/Domain/Order.cs", finding.Witness?.File);
    }

    [Fact]
    public void ApprovedScopedWaiverTemporarilySuppressesOnlyMatchingFinding()
    {
        var (graph, edge) = Fixture();
        var waiver = new ArchitectureWaiver(Rule.Id, edge.Id, "maintainer",
            "Temporary change approved in ADR-004", DateTimeOffset.Parse("2027-01-01T00:00:00Z"));
        var result = ArchitectureGuardian.Evaluate(graph, [Rule], [waiver],
            DateTimeOffset.Parse("2026-11-12T00:00:00Z"));
        Assert.Equal(ArchitectureVerdict.Pass, result.Verdict);
        Assert.False(result.BlocksCi);
        Assert.True(Assert.Single(result.Findings).Waived);
    }

    [Fact]
    public void ExpiredWaiverIsNotEffective()
    {
        var (graph, edge) = Fixture();
        var waiver = new ArchitectureWaiver(Rule.Id, edge.Id, "maintainer",
            "Expired", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var result = ArchitectureGuardian.Evaluate(graph, [Rule], [waiver],
            DateTimeOffset.Parse("2026-11-12T00:00:00Z"));
        Assert.True(result.BlocksCi);
        Assert.False(Assert.Single(result.Findings).Waived);
    }

    [Fact]
    public void AnalysisGapsNeverBecomeFalsePassingAssertions()
    {
        var (graph, _) = Fixture(incomplete: true);
        var rule = Rule with { ToSymbolPrefix = "M:Missing." };
        var result = ArchitectureGuardian.Evaluate(graph, [rule]);
        Assert.Equal(ArchitectureVerdict.Unknown, result.Verdict);
        Assert.NotEmpty(result.UnknownReasons);
    }

    [Fact]
    public void FullyAnalyzedGraphWithNoMatchingViolationPasses()
    {
        var (graph, _) = Fixture();
        var result = ArchitectureGuardian.Evaluate(graph,
            [Rule with { ToSymbolPrefix = "M:Example.Other." }]);
        Assert.Equal(ArchitectureVerdict.Pass, result.Verdict);
        Assert.Empty(result.Findings);
    }
}
