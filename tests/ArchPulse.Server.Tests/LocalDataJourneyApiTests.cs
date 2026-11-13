// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class LocalDataJourneyApiTests
{
    [Fact]
    public async Task ServesOnlyIndexedExactSourceMetadataWithoutWriteRoute()
    {
        using var f = new Fixture(true);
        var response = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal(LocalDataJourneyContract.Schema, response.GetProperty("schema").GetString());
        Assert.Equal(LocalDataJourneyContract.Scope, response.GetProperty("scope").GetString());
        Assert.Equal("Unknown", response.GetProperty("runtimeStatus").GetString());
        Assert.Equal("StaticPossibleRisk",
            response.GetProperty("findings")[0].GetProperty("outcome").GetString());
        using var post = await f.Client.PostAsJsonAsync(Fixture.Route, new { raw = "private" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task MissingReportIsNotMisrepresentedAsNoRisk()
    {
        using var f = new Fixture(false);
        var response = await f.Client.GetFromJsonAsync<JsonElement>(Fixture.Route);
        Assert.Equal("NotIndexed", response.GetProperty("journeyStatus").GetString());
        Assert.Empty(response.GetProperty("findings").EnumerateArray());
        using var missing = await f.Client.GetAsync(
            "/api/graphs/Corpus/missing/datajourney-local");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task CorruptOrForgedSourceEvidenceYieldsUnprocessableEntity()
    {
        using var f = new Fixture(true);
        var path = Assert.Single(System.IO.Directory.GetFiles(f.Directory, "*.datajourney.json"));
        File.WriteAllText(path, File.ReadAllText(path).Replace("StaticPossibleRisk", "VerifiedPrivacy"));
        using var response = await f.Client.GetAsync(Fixture.Route);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Route = "/api/graphs/Corpus/rev/datajourney-local";
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-dj-api-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _host;
        public HttpClient Client { get; }
        public Fixture(bool present)
        {
            var ev = new SourceEvidence("rev", "Flow.cs", 4, 1, EvidenceKind.Static);
            var methodId = "Corpus::M:Example.Flows.Unsafe(System.String)";
            var graph = new GraphSnapshot("Corpus", "rev",
                [new("project:Corpus", GraphNodeKind.Project, "Corpus", null, ev),
                 new(methodId, GraphNodeKind.Method, "Unsafe", "project:Corpus", ev)], [], []);
            new GraphSnapshotStore(Directory).Save(graph);
            if (present)
            {
                var report = new LocalDataJourneyReport(LocalDataJourneyContract.Schema,
                    graph.Project, graph.Revision, LocalDataJourneyContract.Scope,
                    LocalDataJourneyContract.RuntimeStatus,
                    [new LocalDataFlowFinding(methodId, "annotated-sink:M:Foo.Sink",
                        LocalFlowDestination.DeclaredSink, LocalFlowOutcome.StaticPossibleRisk,
                        methodId + "::parameter:token", ev)], []);
                new LocalDataJourneyStore(Directory).Save(graph, report);
            }
            _host = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, cfg) =>
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    { ["ARCHPULSE_GRAPH_STORE"] = Directory })));
            Client = _host.CreateClient();
        }
        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
            System.IO.Directory.Delete(Directory, true);
        }
    }
}
