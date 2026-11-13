// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class FeatureReviewJournalTests
{
    private static GraphSnapshot Graph(string revision = "rev")
    {
        var source = new SourceEvidence(revision, "Invoice.cs", 4, 1, EvidenceKind.Static);
        return new GraphSnapshot("Billing", revision,
        [
            new("project:Billing", GraphNodeKind.Project, "Billing", null, source),
            new("method:Submit", GraphNodeKind.Method, "Submit", "project:Billing", source),
            new("method:Store", GraphNodeKind.Method, "Store", "project:Billing", source)
        ], [], []);
    }

    private static FeatureCatalog Catalog(GraphSnapshot g) =>
        new(FeatureAtlas.Schema, g.Project, g.Revision,
        [
            new("create-invoice", "Create Invoice", "Manual candidate", "Accounts",
                FeatureCriticality.P0, ["Submit"], ["Human review required"],
                [new("method:Submit", "Potential entry", EvidenceKind.Inferred),
                 new("method:Store", "Potential store", EvidenceKind.Designed)])
        ]);

    private static FeatureReviewRequest Request(FeatureReviewAction action,
        string head = FeatureReviewJournal.Genesis, string symbol = "method:Submit",
        int? rollback = null, FeatureReviewActorKind actor = FeatureReviewActorKind.LocalHumanDeclared) =>
        new(action, actor, "Fixture Reviewer", "Reviewed source declaration, candidate only.",
            head, action == FeatureReviewAction.Rollback ? null : "create-invoice",
            action == FeatureReviewAction.Rollback ? null : symbol, rollback);

    [Fact]
    public void EmptyJournalDoesNotFabricateApprovedHumanReviewOrSourceVerification()
    {
        using var temp = new Temp();
        var g = Graph();
        var summary = new FeatureReviewJournal(temp.Path).Read(g, Catalog(g));
        Assert.Equal(0, summary.Sequence);
        Assert.Equal(FeatureReviewJournal.Genesis, summary.Head);
        Assert.Equal(FeatureReviewJournal.Scope, summary.Scope);
        Assert.Empty(summary.History);
        Assert.All(summary.Members, m =>
            Assert.Equal(FeatureReviewDisposition.Unreviewed, m.Disposition));
        Assert.All(Catalog(g).Features[0].Members,
            m => Assert.Contains(m.Claim, new[] { EvidenceKind.Inferred, EvidenceKind.Designed }));
    }

    [Fact]
    public void ExplicitOperatorApprovalPreservesOriginalUnverifiedBusinessClaimAndHistory()
    {
        using var temp = new Temp();
        var g = Graph();
        var catalog = Catalog(g);
        var journal = new FeatureReviewJournal(temp.Path);
        var approved = journal.Append(g, catalog, Request(FeatureReviewAction.ApproveCandidate));
        Assert.Equal(1, approved.Sequence);
        Assert.NotEqual(FeatureReviewJournal.Genesis, approved.Head);
        Assert.Equal(64, approved.Head.Length);
        Assert.Equal("rev", approved.GraphRevision);
        Assert.Equal(FeatureReviewDisposition.OperatorApprovedCandidate,
            Assert.Single(approved.Members, m => m.SymbolId == "method:Submit").Disposition);
        Assert.Equal(FeatureReviewDisposition.Unreviewed,
            Assert.Single(approved.Members, m => m.SymbolId == "method:Store").Disposition);
        Assert.Equal(EvidenceKind.Inferred, catalog.Features[0].Members[0].Claim);
        Assert.Equal(FeatureReviewJournal.Genesis, approved.History[0].PreviousHash);
        Assert.Equal(approved.Head, journal.Read(g, catalog).Head);
        Assert.Equal(FeatureReviewActorKind.LocalHumanDeclared, approved.History[0].ActorKind);
    }

    [Fact]
    public void AutomationNeverOverwritesHumanDecisionEvenIfItKnowsCurrentHead()
    {
        using var temp = new Temp();
        var g = Graph();
        var journal = new FeatureReviewJournal(temp.Path);
        var reviewed = journal.Append(g, Catalog(g), Request(FeatureReviewAction.ApproveCandidate));
        Assert.Throws<InvalidDataException>(() => journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.RejectCandidate, reviewed.Head,
                actor: FeatureReviewActorKind.Automation)));
        Assert.Throws<InvalidDataException>(() => journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.ApproveCandidate, reviewed.Head,
                actor: FeatureReviewActorKind.Unspecified)));
        Assert.Equal(reviewed.Head, journal.Read(g, Catalog(g)).Head);
        Assert.Single(journal.Read(g, Catalog(g)).History);
    }

    [Fact]
    public void ExplicitHumanRollbackCreatesNewReceiptRatherThanErasingPriorApproval()
    {
        using var temp = new Temp();
        var g = Graph();
        var journal = new FeatureReviewJournal(temp.Path);
        var approved = journal.Append(g, Catalog(g), Request(FeatureReviewAction.ApproveCandidate));
        var rejected = journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.RejectCandidate, approved.Head));
        Assert.Equal(FeatureReviewDisposition.OperatorRejectedCandidate,
            Assert.Single(rejected.Members, m => m.SymbolId == "method:Submit").Disposition);
        var rollback = journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.Rollback, rejected.Head, rollback: 1));
        Assert.Equal(3, rollback.Sequence);
        Assert.Equal(rejected.Head, rollback.History[2].PreviousHash);
        Assert.Equal(FeatureReviewAction.Rollback, rollback.History[2].Action);
        Assert.Equal(1, rollback.History[2].RollbackToSequence);
        Assert.Equal(FeatureReviewDisposition.OperatorApprovedCandidate,
            Assert.Single(rollback.Members, m => m.SymbolId == "method:Submit").Disposition);
        Assert.Equal(3, Directory.GetFiles(temp.Path, "*.review-receipt.json",
            SearchOption.AllDirectories).Length);
        var reset = journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.Rollback, rollback.Head, rollback: 0));
        Assert.All(reset.Members,
            m => Assert.Equal(FeatureReviewDisposition.Unreviewed, m.Disposition));
        Assert.Equal(4, reset.History.Count);
    }

    [Fact]
    public void StaleHeadAndConcurrentRequestsCannotSilentlyOverwriteRevision()
    {
        using var temp = new Temp();
        var g = Graph();
        var journal = new FeatureReviewJournal(temp.Path);
        var approved = journal.Append(g, Catalog(g), Request(FeatureReviewAction.ApproveCandidate));
        Assert.Throws<InvalidOperationException>(() => journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.RejectCandidate)));
        Assert.Equal(approved.Head, journal.Read(g, Catalog(g)).Head);
    }

    [Fact]
    public void ForgedReceiptsAndDeletedMiddleSequenceFailClosed()
    {
        using var temp = new Temp();
        var g = Graph();
        var journal = new FeatureReviewJournal(temp.Path);
        var first = journal.Append(g, Catalog(g), Request(FeatureReviewAction.ApproveCandidate));
        journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.RejectCandidate, first.Head, "method:Store"));
        var files = Directory.GetFiles(temp.Path, "*.review-receipt.json",
            SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        File.WriteAllText(files[0], File.ReadAllText(files[0])
            .Replace("Fixture Reviewer", "Impostor Reviewer"));
        Assert.Throws<InvalidDataException>(() => journal.Read(g, Catalog(g)));
        File.Delete(files[0]);
        Assert.Throws<InvalidDataException>(() => journal.Read(g, Catalog(g)));
    }

    [Fact]
    public void RefusesMissingMemberWrongRevisionAndMalformedRollback()
    {
        using var temp = new Temp();
        var g = Graph();
        var journal = new FeatureReviewJournal(temp.Path);
        Assert.Throws<InvalidDataException>(() => journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.ApproveCandidate, symbol: "method:Fabricated")));
        Assert.Throws<InvalidDataException>(() => journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.Rollback, rollback: 0)));
        Assert.Throws<InvalidDataException>(() => journal.Append(g, Catalog(g),
            Request(FeatureReviewAction.Unspecified)));
        Assert.Throws<InvalidDataException>(() => journal.Read(Graph("other"), Catalog(g)));
    }

    [Fact]
    public void ExactCatalogDigestSeparatesDistinctCandidateDefinitionsWithoutDeletingHistory()
    {
        using var temp = new Temp();
        var g = Graph();
        var catalog = Catalog(g);
        var journal = new FeatureReviewJournal(temp.Path);
        var review = journal.Append(g, catalog, Request(FeatureReviewAction.ApproveCandidate));
        var revised = catalog with { Features = [catalog.Features[0] with
            { Description = "Different proposed intent" }] };
        var alternate = journal.Read(g, revised);
        Assert.Equal(0, alternate.Sequence);
        Assert.NotEqual(review.CatalogSha256, alternate.CatalogSha256);
        Assert.Equal(review.Head, journal.Read(g, catalog).Head);
    }

    [Fact]
    public void ParsesExplicitBoundedRequestAndRejectsMissingActionAndActor()
    {
        var valid = """
          {"action":"ApproveCandidate","actorKind":"LocalHumanDeclared",
           "actor":"Fixture Reviewer","reason":"Source review only",
           "expectedHead":"GENESIS","featureId":"create-invoice","symbolId":"method:Submit"}
          """;
        var obj = FeatureReviewJournal.ParseRequest(valid);
        Assert.Equal(FeatureReviewAction.ApproveCandidate, obj.Action);
        Assert.Equal(FeatureReviewActorKind.LocalHumanDeclared, obj.ActorKind);
        using var temp = new Temp();
        var g = Graph();
        Assert.Single(new FeatureReviewJournal(temp.Path).Append(g, Catalog(g), obj).History);
        var partial = FeatureReviewJournal.ParseRequest(
            """{"actor":"A","reason":"B","expectedHead":"GENESIS","featureId":"create-invoice","symbolId":"method:Submit"}""");
        Assert.Throws<InvalidDataException>(() =>
            new FeatureReviewJournal(temp.Path).Append(g, Catalog(g),
                partial with { ExpectedHead = new FeatureReviewJournal(temp.Path)
                    .Read(g, Catalog(g)).Head }));
    }

    private sealed class Temp : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-review-tests-" + Guid.NewGuid().ToString("N"));
        public Temp() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
