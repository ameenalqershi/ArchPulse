// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.McpServer;
using Xunit;

namespace ArchPulse.McpServer.Tests;

public sealed class McpFeatureReviewTests
{
    [Fact]
    public void ReadLocalHumanDeclaredReviewsWithoutPromotingBusinessVerification()
    {
        using var f = new Fixture(true);
        var result = GraphTools.FeatureReviews(f.Graphs, f.Catalogs, f.Journal, "Billing", "rev");
        Assert.NotEqual(true, result.IsError);
        var json = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(FeatureReviewJournal.Scope, json.GetProperty("scope").GetString());
        Assert.Equal(1, json.GetProperty("sequence").GetInt32());
        var item = Assert.Single(json.GetProperty("members").EnumerateArray());
        Assert.Equal("OperatorApprovedCandidate", item.GetProperty("disposition").GetString());
        Assert.Equal("LocalHumanDeclared",
            json.GetProperty("history")[0].GetProperty("actorKind").GetString());
        Assert.Equal("GENESIS",
            json.GetProperty("history")[0].GetProperty("previousHash").GetString());
    }

    [Fact]
    public void MissingCatalogMeansNoReviewAssertions()
    {
        using var f = new Fixture(false);
        var result = GraphTools.FeatureReviews(f.Graphs, f.Catalogs, f.Journal, "Billing", "rev");
        Assert.NotEqual(true, result.IsError);
        Assert.Equal("NotIndexed", Assert.IsType<JsonElement>(result.StructuredContent)
            .GetProperty("reviewStatus").GetString());
        Assert.True(GraphTools.FeatureReviews(f.Graphs, f.Catalogs, f.Journal,
            "Billing", "absent").IsError);
    }

    [Fact]
    public void TamperingIsNeverInterpretedAsAnApprovedReview()
    {
        using var f = new Fixture(true);
        var file = Assert.Single(Directory.GetFiles(f.Directory,
            "*.review-receipt.json", SearchOption.AllDirectories));
        File.WriteAllText(file, File.ReadAllText(file)
            .Replace("Local Operator", "Forged Operator"));
        Assert.True(GraphTools.FeatureReviews(f.Graphs, f.Catalogs, f.Journal,
            "Billing", "rev").IsError);
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-feature-review-mcp-" + Guid.NewGuid().ToString("N"));
        public GraphSnapshotStore Graphs { get; }
        public FeatureCatalogStore Catalogs { get; }
        public FeatureReviewJournal Journal { get; }
        public Fixture(bool withCatalog)
        {
            var e = new SourceEvidence("rev", "Invoice.cs", 4, 1, EvidenceKind.Static);
            Graphs = new GraphSnapshotStore(Directory);
            Catalogs = new FeatureCatalogStore(Directory);
            Journal = new FeatureReviewJournal(Directory);
            var graph = new GraphSnapshot("Billing", "rev",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, e),
                new("method:Entry", GraphNodeKind.Method, "Entry", "project:Billing", e)
            ], [], []);
            Graphs.Save(graph);
            if (withCatalog)
            {
                var catalog = new FeatureCatalog(FeatureAtlas.Schema, "Billing", "rev",
                [
                    new BusinessFeature("create-invoice", "Create Invoice",
                        "Candidate", "Accounts", FeatureCriticality.P0,
                        ["Create"], ["Review"], [
                            new FeatureMembership("method:Entry", "Candidate",
                                EvidenceKind.Inferred)])
                ]);
                Catalogs.Save(graph, catalog);
                Journal.Append(graph, catalog, new FeatureReviewRequest(
                    FeatureReviewAction.ApproveCandidate, FeatureReviewActorKind.LocalHumanDeclared,
                    "Local Operator", "Review logged locally; identity unverified.",
                    FeatureReviewJournal.Genesis, "create-invoice", "method:Entry"));
            }
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
