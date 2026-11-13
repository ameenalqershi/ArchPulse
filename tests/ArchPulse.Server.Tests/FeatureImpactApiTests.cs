// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class FeatureImpactApiTests
{
    [Fact]
    public async Task ReadOnlyExactRevisionImpactReportsStaticWitnessWithoutRuntimeClaims()
    {
        using var f = new Fixture(true);
        var json = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("archpulse.feature-impact/v1", json.GetProperty("schema").GetString());
        Assert.Equal("old", json.GetProperty("fromRevision").GetString());
        Assert.Equal("new", json.GetProperty("toRevision").GetString());
        Assert.Equal("Unknown", json.GetProperty("runtimeStatus").GetString());
        Assert.Equal(2, json.GetProperty("structuralChangesExamined").GetInt32());
        Assert.Equal(2, json.GetProperty("impacts").GetArrayLength());
        var added = Assert.Single(json.GetProperty("impacts").EnumerateArray(),
            x => x.GetProperty("changeKind").GetString() == "NodeAdded");
        Assert.Equal("StaticCallerPath", added.GetProperty("basis").GetString());
        Assert.Equal("Static", added.GetProperty("staticCallWitness")[0]
            .GetProperty("evidence").GetProperty("kind").GetString());
        using var post = await f.Client.PostAsJsonAsync(Fixture.Route,
            new { featureApproved = true });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task MissingCatalogDoesNotAssertZeroBusinessImpact()
    {
        using var f = new Fixture(false);
        var json = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("NotIndexed", json.GetProperty("catalogStatus").GetString());
        Assert.Equal(0, json.GetProperty("impacts").GetArrayLength());
        Assert.Equal("Unknown", json.GetProperty("runtimeStatus").GetString());
    }

    [Fact]
    public async Task UnknownRevisionAndCorruptedCatalogFailClosed()
    {
        using var f = new Fixture(true);
        using var missing = await f.Client.GetAsync("/api/graphs/Billing/feature-impact?from=old&to=unknown");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var file = Assert.Single(Directory.GetFiles(f.Directory, "*.features.json"));
        File.WriteAllText(file, File.ReadAllText(file)
            .Replace("method:Entry", "method:Forged", StringComparison.Ordinal));
        using var invalid = await f.Client.GetAsync(Fixture.Route);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Route = "/api/graphs/Billing/feature-impact?from=old&to=new";
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-impact-api-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }

        public Fixture(bool withCatalog)
        {
            var oldEv = new SourceEvidence("old", "Billing.cs", 7, 1, EvidenceKind.Static);
            var newEv = oldEv with { Revision = "new" };
            var old = new GraphSnapshot("Billing", "old",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, oldEv),
                new("method:Entry", GraphNodeKind.Method, "Entry", "project:Billing", oldEv)
            ], [], []);
            var updated = new GraphSnapshot("Billing", "new",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, newEv),
                new("method:Entry", GraphNodeKind.Method, "Entry", "project:Billing", newEv),
                new("method:Changed", GraphNodeKind.Method, "Changed", "project:Billing", newEv)
            ], [new("new-call", "method:Entry", "method:Changed", "Calls", newEv)], []);
            var store = new GraphSnapshotStore(Directory);
            store.Save(old);
            store.Save(updated);
            if (withCatalog)
                new FeatureCatalogStore(Directory).Save(old,
                    new FeatureCatalog(FeatureAtlas.Schema, "Billing", "old",
                    [
                        new BusinessFeature("create-invoice", "Create Invoice", "Candidate",
                            "Billing", FeatureCriticality.P0, ["Submit"],
                            ["Review"], [new FeatureMembership("method:Entry",
                                "Proposed start", EvidenceKind.Designed)])
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
