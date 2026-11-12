// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class FeatureSignalsApiTests
{
    [Fact]
    public async Task NoReportIsNotIndexedRatherThanProofOfNoFeatures()
    {
        using var f = new Fixture(false);
        var json = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("NotIndexed", json.GetProperty("discoveryStatus").GetString());
        Assert.Equal(0, json.GetProperty("signals").GetArrayLength());
        Assert.Equal("Unknown", json.GetProperty("runtimeStatus").GetString());
    }

    [Fact]
    public async Task ExactSourceSignalIsInferredAndReadOnly()
    {
        using var f = new Fixture(true);
        var json = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("IndexedCandidates", json.GetProperty("discoveryStatus").GetString());
        Assert.Equal(FeatureDiscoveryContract.Scope, json.GetProperty("scope").GetString());
        var candidate = Assert.Single(json.GetProperty("signals").EnumerateArray());
        Assert.Equal("Inferred", candidate.GetProperty("businessClaim").GetString());
        Assert.Equal("Static", candidate.GetProperty("source").GetProperty("kind").GetString());
        Assert.Equal("NotRegisteredRoute", candidate.GetProperty("detector").GetString()?
            .Split(';').Last());
        using var forbidden = await f.Client.PostAsJsonAsync(Fixture.Route,
            new { claim = "Verified" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, forbidden.StatusCode);
    }

    [Fact]
    public async Task MissingRevisionAndCorruptedEvidenceAreNotPublishedAsReal()
    {
        using var f = new Fixture(true);
        using var missing = await f.Client.GetAsync("/api/graphs/Billing/other/feature-signals");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var path = Assert.Single(Directory.GetFiles(f.Directory, "*.feature-signals.json"));
        File.WriteAllText(path, File.ReadAllText(path).Replace("feature-signal:", "forged:"));
        using var invalid = await f.Client.GetAsync(Fixture.Route);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Route = "/api/graphs/Billing/rev/feature-signals";
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-signal-api-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }
        public Fixture(bool withReport)
        {
            var evidence = new SourceEvidence("rev", "Invoice.cs", 1, 1, EvidenceKind.Static);
            var graph = new GraphSnapshot("Billing", "rev",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, evidence),
                new("Billing::M:InvoicesController.CreateInvoice",
                    GraphNodeKind.Method, "CreateInvoice()", "project:Billing", evidence)
            ], [], []);
            new GraphSnapshotStore(Directory).Save(graph);
            if (withReport)
                new FeatureDiscoveryStore(Directory).Save(graph,
                    new FeatureDiscoveryReport(FeatureDiscoveryContract.Schema, "Billing", "rev",
                        FeatureDiscoveryContract.Scope, "Unknown",
                        [
                            new FeatureCandidateSignal(
                                "feature-signal:ControllerAction:Billing::M:InvoicesController.CreateInvoice",
                                FeatureSignalKind.ControllerAction,
                                "Billing::M:InvoicesController.CreateInvoice", "create-invoice",
                                "KnownMvcHttpAttributeName;NotRegisteredRoute",
                                "Attribute-name signal, not verified registration.",
                                evidence, EvidenceKind.Inferred, "POST", null)
                        ], []));
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string,string?>
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
