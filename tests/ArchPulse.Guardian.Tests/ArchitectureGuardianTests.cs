// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Guardian;
using Xunit;

namespace ArchPulse.Guardian.Tests;

public sealed class ArchitectureGuardianTests
{
    private static GraphSnapshot Graph(EvidenceKind kind = EvidenceKind.Static)
    {
        var evidence = new SourceEvidence("sha1", "Billing.Service/InvoiceService.cs", 12, 3, kind);
        return new GraphSnapshot("Billing", "sha1",
        [
            new("solution:Billing", GraphNodeKind.Solution, "Billing", null, evidence),
            new("project:Billing.Service", GraphNodeKind.Project, "Billing.Service", "solution:Billing", evidence),
            new("project:Billing.Contracts", GraphNodeKind.Project, "Billing.Contracts", "solution:Billing", evidence),
            new("Billing.Service::M:Billing.Service.InvoiceService.CreateInvoice",
                GraphNodeKind.Method, "CreateInvoice", "project:Billing.Service", evidence),
            new("Billing.Contracts::M:Billing.Contracts.RateCalculator.Apply",
                GraphNodeKind.Method, "Apply", "project:Billing.Contracts", evidence)
        ],
        [
            new("call:001",
                "Billing.Service::M:Billing.Service.InvoiceService.CreateInvoice",
                "Billing.Contracts::M:Billing.Contracts.RateCalculator.Apply", "Calls", evidence)
        ], []);
    }

    private static ForbiddenDependencyRule Rule => new(
        "NO-SERVICE-CONTRACT-CALL", "v1", "Billing.Service",
        "Billing.Contracts", "Calls", RuleSeverity.Blocker);

    [Fact]
    public void SourceBackedForbiddenCallEmitsExplainableViolation()
    {
        var finding = Assert.Single(ArchitectureGuardian.Evaluate(Graph(), [Rule]));
        Assert.Equal(RuleOutcome.Violation, finding.Outcome);
        Assert.Equal("call:001", finding.EdgeId);
        Assert.Equal(RuleSeverity.Blocker, finding.Severity);
        Assert.Equal("sha1", finding.Evidence?.Revision);
        Assert.Contains("InvoiceService.cs:12", finding.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void InferredRelationIsUnknownRatherThanProvenViolation()
    {
        var finding = Assert.Single(ArchitectureGuardian.Evaluate(Graph(EvidenceKind.Inferred), [Rule]));
        Assert.Equal(RuleOutcome.Unknown, finding.Outcome);
    }

    [Fact]
    public void ApprovedUnexpiredWaiverDoesNotDeleteEvidence()
    {
        var now = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
        var waiver = new HumanWaiver(Rule.Id, "call:001", "owner", "Approved until migration",
            now.AddDays(2));
        var finding = Assert.Single(ArchitectureGuardian.Evaluate(Graph(), [Rule], [waiver], now));
        Assert.Equal(RuleOutcome.Waived, finding.Outcome);
        Assert.Equal("call:001", finding.EdgeId);
    }

    [Fact]
    public void ExpiredWaiverIsStillViolation()
    {
        var now = DateTimeOffset.UtcNow;
        var waiver = new HumanWaiver(Rule.Id, "call:001", "owner", "Expired",
            now.AddMinutes(-1));
        Assert.Equal(RuleOutcome.Violation,
            Assert.Single(ArchitectureGuardian.Evaluate(Graph(), [Rule], [waiver], now)).Outcome);
    }

    [Fact]
    public void UnknownProjectIsNotEvaluatedNotPass()
    {
        var rule = Rule with { ToProject = "Unknown.Project" };
        Assert.Equal(RuleOutcome.NotEvaluated,
            Assert.Single(ArchitectureGuardian.Evaluate(Graph(), [rule])).Outcome);
    }

    [Fact]
    public void NonMatchingRelationDoesNotCreateFalseViolation()
    {
        var rule = Rule with { Relation = "ObservedCall" };
        Assert.Equal(RuleOutcome.NotEvaluated,
            Assert.Single(ArchitectureGuardian.Evaluate(Graph(), [rule])).Outcome);
    }

    [Fact]
    public void ForbiddenRelationBetweenOpaqueSymbolIdsUsesCanonicalParentOwnership()
    {
        var source = new SourceEvidence("sha1", "Different.cs", 7, 2, EvidenceKind.Static);
        var graph = Graph() with
        {
            Nodes =
            [
                .. Graph().Nodes.Take(3),
                new GraphNode("opaque-source", GraphNodeKind.Method, "OpaqueSource",
                    "project:Billing.Service", source),
                new GraphNode("opaque-destination", GraphNodeKind.Method, "OpaqueDestination",
                    "project:Billing.Contracts", source)
            ],
            Edges = [new GraphEdge("opaque-call", "opaque-source", "opaque-destination",
                "Calls", source)]
        };

        var finding = Assert.Single(ArchitectureGuardian.Evaluate(graph, [Rule]));
        Assert.Equal(RuleOutcome.Violation, finding.Outcome);
        Assert.Equal("opaque-call", finding.EdgeId);
    }

    [Fact]
    public void MisleadingSymbolPrefixesCannotForgeCanonicalProjectOwnership()
    {
        var source = new SourceEvidence("sha1", "Different.cs", 9, 2, EvidenceKind.Static);
        var graph = Graph() with
        {
            Nodes =
            [
                .. Graph().Nodes.Take(3),
                new GraphNode("Billing.Service::M:actually-contracts", GraphNodeKind.Method,
                    "Misleading source", "project:Billing.Contracts", source),
                new GraphNode("Billing.Contracts::M:actually-service", GraphNodeKind.Method,
                    "Misleading target", "project:Billing.Service", source)
            ],
            Edges = [new GraphEdge("reversed-owner-call",
                "Billing.Service::M:actually-contracts",
                "Billing.Contracts::M:actually-service", "Calls", source)]
        };

        var finding = Assert.Single(ArchitectureGuardian.Evaluate(graph, [Rule]));
        Assert.Equal(RuleOutcome.NotEvaluated, finding.Outcome);
        Assert.Null(finding.EdgeId);
    }

    [Fact]
    public void NestedSymbolsResolveTheirRealOwningProjectsWithoutNamePrefixes()
    {
        var source = new SourceEvidence("sha1", "Nested.cs", 4, 1, EvidenceKind.Static);
        var graph = Graph() with
        {
            Nodes =
            [
                .. Graph().Nodes.Take(3),
                new GraphNode("namespace-a", GraphNodeKind.Namespace, "Namespace A",
                    "project:Billing.Service", source),
                new GraphNode("class-a", GraphNodeKind.Class, "Class A", "namespace-a", source),
                new GraphNode("method-a", GraphNodeKind.Method, "A", "class-a", source),
                new GraphNode("namespace-b", GraphNodeKind.Namespace, "Namespace B",
                    "project:Billing.Contracts", source),
                new GraphNode("class-b", GraphNodeKind.Class, "Class B", "namespace-b", source),
                new GraphNode("method-b", GraphNodeKind.Method, "B", "class-b", source)
            ],
            Edges = [new GraphEdge("nested-call", "method-a", "method-b", "Calls", source)]
        };

        var finding = Assert.Single(ArchitectureGuardian.Evaluate(graph, [Rule]));
        Assert.Equal(RuleOutcome.Violation, finding.Outcome);
        Assert.Equal("nested-call", finding.EdgeId);
    }

    [Fact]
    public void DuplicateProjectLabelsMakeNameScopedRuleUnresolvableNotProven()
    {
        var source = new SourceEvidence("sha1", "Ambiguous.cs", 1, 1, EvidenceKind.Static);
        var graph = Graph() with
        {
            Nodes =
            [
                .. Graph().Nodes,
                new GraphNode("different-project-id", GraphNodeKind.Project,
                    "Billing.Service", "solution:Billing", source)
            ]
        };

        var finding = Assert.Single(ArchitectureGuardian.Evaluate(graph, [Rule]));
        Assert.Equal(RuleOutcome.NotEvaluated, finding.Outcome);
        Assert.Null(finding.EdgeId);
        Assert.Contains("ambiguous", finding.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisabledRuleDoesNotEmitFindings()
    {
        Assert.Empty(ArchitectureGuardian.Evaluate(Graph(), [Rule with { Enabled = false }]));
    }
}
