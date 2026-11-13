// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class FeatureReviewApiTests
{
    [Fact]
    public async Task ReadOnlyReviewPreservesCandidateScopeAndLocalHumanDeclaration()
    {
        using var f = new Fixture(true);
        var json = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal(FeatureReviewJournal.Schema, json.GetProperty("schema").GetString());
        Assert.Equal(FeatureReviewJournal.Scope, json.GetProperty("scope").GetString());
        Assert.Equal(1, json.GetProperty("sequence").GetInt32());
        var member = Assert.Single(json.GetProperty("members").EnumerateArray());
        Assert.Equal("OperatorApprovedCandidate", member.GetProperty("disposition").GetString());
        var receipt = Assert.Single(json.GetProperty("history").EnumerateArray());
        Assert.Equal("LocalHumanDeclared", receipt.GetProperty("actorKind").GetString());
        using var post = await f.Client.PostAsJsonAsync(Fixture.Route, new { approve = true });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task MissingGraphOrCatalogDoesNotInventApprovedReviews()
    {
        using var f = new Fixture(false);
        var json = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("NotIndexed", json.GetProperty("catalogStatus").GetString());
        using var missing = await f.Client.GetAsync(
            "/api/graphs/Billing/not-indexed/feature-reviews");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ForgedAuditHistoryReturnsValidationFailure()
    {
        using var f = new Fixture(true);
        var file = Assert.Single(Directory.GetFiles(f.Directory,
            "*.review-receipt.json", SearchOption.AllDirectories));
        File.WriteAllText(file, File.ReadAllText(file)
            .Replace("Review Operator", "Other Operator"));
        using var invalid = await f.Client.GetAsync(Fixture.Route);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Route = "/api/graphs/Billing/rev/feature-reviews";
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-feature-review-api-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }
        public Fixture(bool withCatalog)
        {
            var e = new SourceEvidence("rev", "Invoices.cs", 3, 1, EvidenceKind.Static);
            var graph = new GraphSnapshot("Billing", "rev",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, e),
                new("method:Entry", GraphNodeKind.Method, "Entry", "project:Billing", e)
            ], [], []);
            new GraphSnapshotStore(Directory).Save(graph);
            if (withCatalog)
            {
                var catalog = new FeatureCatalog(FeatureAtlas.Schema, "Billing", "rev",
                [
                    new BusinessFeature("create-invoice", "Create Invoice",
                        "Local candidate mapping", "Accounts", FeatureCriticality.P0,
                        ["Create"], ["Review"], [
                            new FeatureMembership("method:Entry", "Operator claim",
                                EvidenceKind.Designed)])
                ]);
                new FeatureCatalogStore(Directory).Save(graph, catalog);
                new FeatureReviewJournal(Directory).Append(graph, catalog,
                    new FeatureReviewRequest(FeatureReviewAction.ApproveCandidate,
                        FeatureReviewActorKind.LocalHumanDeclared,
                        "Review Operator", "Locally declared and inspectable.",
                        FeatureReviewJournal.Genesis, "create-invoice", "method:Entry"));
            }
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    { ["ARCHPULSE_GRAPH_STORE"] = Directory })));
            Client = _factory.CreateClient();
        }
        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            System.IO.Directory.Delete(Directory, true);
        }
    }
}
