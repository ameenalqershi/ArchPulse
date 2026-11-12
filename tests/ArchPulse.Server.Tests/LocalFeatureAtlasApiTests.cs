// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class LocalFeatureAtlasApiTests
{
    [Fact]
    public async Task UnindexedCatalogIsExplicitRatherThanAnEmptyVerifiedFeatureMap()
    {
        using var fixture = new Fixture(withCatalog: false);
        var listing = await fixture.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("NotIndexed", listing.GetProperty("catalogStatus").GetString());
        Assert.Equal(0, listing.GetProperty("features").GetArrayLength());
        Assert.Equal("Unknown", listing.GetProperty("executionStatus").GetString());
        Assert.False(listing.GetProperty("observedCoverage").GetBoolean());
    }

    [Fact]
    public async Task ExactFeatureReturnsManualMembershipAndStaticEdgesOnly()
    {
        using var fixture = new Fixture(withCatalog: true);
        var listing = await fixture.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("OperatorCurated", listing.GetProperty("catalogStatus").GetString());
        var view = await fixture.Client.GetFromJsonAsync<JsonElement>(Fixture.Route + "/create-invoice");
        Assert.Equal(FeatureAtlas.ScopeSchema, view.GetProperty("schema").GetString());
        Assert.Equal("rev-1", view.GetProperty("revision").GetString());
        Assert.Equal("create-invoice", view.GetProperty("feature").GetProperty("id").GetString());
        Assert.Equal(2, view.GetProperty("nodes").GetArrayLength());
        Assert.Single(view.GetProperty("staticEdges").EnumerateArray());
        Assert.Equal("Designed", view.GetProperty("feature").GetProperty("members")[0]
            .GetProperty("claim").GetString());
        Assert.Equal("ManualCandidateMembership;StaticInternalGraphEdgesOnly",
            view.GetProperty("membershipScope").GetString());
        Assert.False(view.GetProperty("observedCoverage").GetBoolean());
    }

    [Fact]
    public async Task UnknownFeatureRevisionAndWritesRemainUnavailable()
    {
        using var fixture = new Fixture(withCatalog: true);
        using var unknown = await fixture.Client.GetAsync(Fixture.Route + "/refund");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var wrong = await fixture.Client.GetAsync("/api/graphs/Billing/rev-2/features/create-invoice");
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        using var post = await fixture.Client.PostAsJsonAsync(Fixture.Route, new { pretendVerified = true });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task CorruptedCatalogMustNotBeReportedAsValid()
    {
        using var fixture = new Fixture(withCatalog: true);
        var file = Assert.Single(Directory.GetFiles(fixture.Directory, "*.features.json"));
        File.WriteAllText(file, File.ReadAllText(file)
            .Replace("method:Invoice.Create", "method:Nonexistent", StringComparison.Ordinal));
        using var response = await fixture.Client.GetAsync(Fixture.Route);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Route = "/api/graphs/Billing/rev-1/features";
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-feature-api-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }

        public Fixture(bool withCatalog)
        {
            var ev = new SourceEvidence("rev-1", "Invoice.cs", 1, 1, EvidenceKind.Static);
            var graph = new GraphSnapshot("Billing", "rev-1",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, ev),
                new("method:Invoice.Create", GraphNodeKind.Method, "Create", "project:Billing", ev),
                new("method:Invoice.Save", GraphNodeKind.Method, "Save", "project:Billing", ev)
            ],
            [new("call", "method:Invoice.Create", "method:Invoice.Save", "Calls", ev)], []);
            new GraphSnapshotStore(Directory).Save(graph);
            if (withCatalog)
                new FeatureCatalogStore(Directory).Save(graph,
                    new FeatureCatalog(FeatureAtlas.Schema, "Billing", "rev-1",
                    [
                        new BusinessFeature("create-invoice", "Create Invoice",
                            "Curated mapping, not runtime evidence", "Owner", FeatureCriticality.P0,
                            ["creates"], ["requires review"], [
                                new("method:Invoice.Create", "candidate entry", EvidenceKind.Designed),
                                new("method:Invoice.Save", "candidate persistence", EvidenceKind.Inferred)
                            ])
                    ]));
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    { ["ARCHPULSE_GRAPH_STORE"] = Directory })));
            Client = _factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
