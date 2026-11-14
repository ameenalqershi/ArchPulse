// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class CrossProjectDataJourneyApiTests
{
    [Fact]
    public async Task ReadsExactMethodWitnessWithReturnDirectionAndNoWriteRoute()
    {
        using var fixture = new Fixture();
        fixture.Journeys.Save(fixture.Graph, fixture.Report);
        fixture.Journeys.Save(fixture.Graph, fixture.Report);

        using var response = await fixture.Client.GetAsync(Fixture.RouteFor(Fixture.Callee));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mapped", body.GetProperty("status").GetString());
        Assert.Equal("Unknown", body.GetProperty("runtimeStatus").GetString());
        var witness = Assert.Single(body.GetProperty("findings").EnumerateArray());
        Assert.Equal("Return", witness.GetProperty("hops")[0].GetProperty("kind").GetString());
        Assert.Equal(Fixture.Callee, witness.GetProperty("hops")[0].GetProperty("call").GetProperty("toSymbolId").GetString());
        Assert.Equal("NoWitness", (await fixture.Client.GetFromJsonAsync<JsonElement>(
            Fixture.RouteFor(Fixture.Unrelated))).GetProperty("status").GetString());
        using var post = await fixture.Client.PostAsJsonAsync(Fixture.RouteFor(Fixture.Callee), new { });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task MissingAndTamperedEvidenceRemainDistinct()
    {
        using var fixture = new Fixture();
        var absent = await fixture.Client.GetFromJsonAsync<JsonElement>(Fixture.RouteFor(Fixture.Caller));
        Assert.Equal("NotIndexed", absent.GetProperty("status").GetString());
        using var wrongMethod = await fixture.Client.GetAsync(Fixture.RouteFor("missing"));
        Assert.Equal(HttpStatusCode.NotFound, wrongMethod.StatusCode);
        using var wrongRevision = await fixture.Client.GetAsync(Fixture.RouteFor(Fixture.Caller, "other"));
        Assert.Equal(HttpStatusCode.NotFound, wrongRevision.StatusCode);

        fixture.Journeys.Save(fixture.Graph, fixture.Report);
        string path = Assert.Single(Directory.GetFiles(fixture.DirectoryPath, "*.datajourney-cross-project.json"));
        File.WriteAllText(path, File.ReadAllText(path).Replace("Unknown", "Observed"));
        using var invalid = await fixture.Client.GetAsync(Fixture.RouteFor(Fixture.Callee));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    [Fact]
    public async Task TruncatedSelectionWithoutWitnessIsIndeterminateNotSafeOrAbsent()
    {
        using var fixture = new Fixture();
        fixture.Journeys.Save(fixture.Graph, fixture.Report with { Truncated = true });

        var unrelated = await fixture.Client.GetFromJsonAsync<JsonElement>(
            Fixture.RouteFor(Fixture.Unrelated));
        Assert.Equal("Indeterminate", unrelated.GetProperty("status").GetString());
        Assert.True(unrelated.GetProperty("truncated").GetBoolean());
        Assert.Equal("Unknown", unrelated.GetProperty("runtimeStatus").GetString());
        Assert.Empty(unrelated.GetProperty("findings").EnumerateArray());

        var mapped = await fixture.Client.GetFromJsonAsync<JsonElement>(
            Fixture.RouteFor(Fixture.Callee));
        Assert.Equal("Mapped", mapped.GetProperty("status").GetString());
        Assert.True(mapped.GetProperty("truncated").GetBoolean());
        Assert.Single(mapped.GetProperty("findings").EnumerateArray());
    }

    [Fact]
    public void RejectsForgedCallEdgesAndSameRevisionReplacement()
    {
        using var fixture = new Fixture();
        var witness = fixture.Report.Findings[0];
        var hop = witness.Hops[0];
        Assert.Throws<InvalidDataException>(() => fixture.Journeys.Save(fixture.Graph,
            fixture.Report with
            {
                Findings = [witness with { Hops = [hop with { Call = hop.Call with { EdgeId = "invented" } }] }]
            }));
        Assert.Throws<InvalidDataException>(() => fixture.Journeys.Save(fixture.Graph,
            fixture.Report with { Findings = [witness with { Hops = [hop with { Kind = DataJourneyHopKind.Argument }] }] }));
        fixture.Journeys.Save(fixture.Graph, fixture.Report);
        Assert.Throws<InvalidOperationException>(() => fixture.Journeys.Save(
            fixture.Graph, fixture.Report with { Truncated = true }));
    }

    private sealed class Fixture : IDisposable
    {
        public const string Caller = "Entry::M:Sample.Entry";
        public const string Callee = "Service::M:Sample.Source";
        public const string Unrelated = "Entry::M:Sample.Unrelated";
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "archpulse-cross-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _host;
        public HttpClient Client { get; }
        public GraphSnapshot Graph { get; }
        public CrossProjectDataJourneyReport Report { get; }
        public CrossProjectDataJourneyStore Journeys { get; }

        public static string RouteFor(string id, string revision = "rev") =>
            "/api/graphs/Corpus/" + revision + "/datajourney-cross-project?symbolId=" + Uri.EscapeDataString(id);

        public Fixture()
        {
            var entry = new SourceEvidence("rev", "Entry.cs", 5, 2, EvidenceKind.Static);
            var service = new SourceEvidence("rev", "Service.cs", 2, 1, EvidenceKind.Static);
            var call = new GraphEdge("call", Caller, Callee, "Calls", entry);
            Graph = new GraphSnapshot("Corpus", "rev",
                [new(Caller, GraphNodeKind.Method, "Entry", null, entry),
                 new(Callee, GraphNodeKind.Method, "Source", null, service),
                 new(Unrelated, GraphNodeKind.Method, "Unrelated", null, entry)], [call], []);
            Report = new(CrossProjectDataJourneyStore.Schema, "Corpus", "rev", EvidenceKind.Static,
                "Unknown", false, [new(Callee, service, [new(DataJourneyHopKind.Return, call)],
                    DataJourneyBoundary.OpaqueCall, entry)]);
            new GraphSnapshotStore(DirectoryPath).Save(Graph);
            Journeys = new(DirectoryPath);
            _host = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ARCHPULSE_GRAPH_STORE"] = DirectoryPath })));
            Client = _host.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
            Directory.Delete(DirectoryPath, true);
        }
    }
}
