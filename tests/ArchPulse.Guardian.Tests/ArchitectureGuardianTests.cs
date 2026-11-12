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
    public void DisabledRuleDoesNotEmitFindings()
    {
        Assert.Empty(ArchitectureGuardian.Evaluate(Graph(), [Rule with { Enabled = false }]));
    }
}
