// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class CanonicalTopologyApiTests
{
    [Fact]
    public async Task ReturnsExactBreadcrumbWithIndependentEvidenceModes()
    {
        using var fixture = new Fixture();
        var result = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Demo/commit1/scope-topology?symbolId=L3");
        Assert.Equal(GraphScopeTopology.ContractSchema,
            result.GetProperty("schema").GetString());
        Assert.Equal("Demo", result.GetProperty("project").GetString());
        Assert.Equal("commit1", result.GetProperty("revision").GetString());
        Assert.Equal("L0", result.GetProperty("rootSymbolId").GetString());
        Assert.Equal("L3", result.GetProperty("symbolId").GetString());
        Assert.Equal(3, result.GetProperty("depth").GetInt32());
        Assert.Equal(new[] { "L0", "L1", "L2", "L3" },
            result.GetProperty("breadcrumb").EnumerateArray()
                .Select(n => n.GetProperty("symbolId").GetString()));
        Assert.Equal("Inferred", result.GetProperty("node").GetProperty("evidence")
            .GetProperty("kind").GetString());
        Assert.Equal(0, result.GetProperty("directChildrenCount").GetInt32());
        Assert.Equal(0, result.GetProperty("descendantCount").GetInt32());

        var parent = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Demo/commit1/scope-topology?symbolId=L2");
        Assert.Equal(1, parent.GetProperty("directChildrenCount").GetInt32());
        var modes = parent.GetProperty("directChildEvidence").EnumerateArray()
            .ToDictionary(e => e.GetProperty("kind").GetString()!, e =>
                e.GetProperty("count").GetInt32());
        Assert.Equal(1, modes["Inferred"]);
        Assert.Equal(0, modes["Verified"]);
    }

    [Fact]
    public async Task UnknownOrInvalidExactSymbolFailsClosedAndNoMutationRouteExists()
    {
        using var fixture = new Fixture();
        string baseUrl = "/api/graphs/Demo/commit1/scope-topology";
        using var absent = await fixture.Client.GetAsync(baseUrl + "?symbolId=unknown");
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        using var blank = await fixture.Client.GetAsync(baseUrl + "?symbolId=%20");
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        using var noParam = await fixture.Client.GetAsync(baseUrl);
        Assert.Equal(HttpStatusCode.BadRequest, noParam.StatusCode);
        using var huge = await fixture.Client.GetAsync(baseUrl + "?symbolId=" + new string('a', 2049));
        Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);
        using var missingRev = await fixture.Client.GetAsync(
            "/api/graphs/Demo/missing/scope-topology?symbolId=L3");
        Assert.Equal(HttpStatusCode.NotFound, missingRev.StatusCode);
        using var write = await fixture.Client.PostAsJsonAsync(
            baseUrl + "?symbolId=L3", new { id = "forged" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(),
            "archpulse-scope-topology-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }

        public Fixture()
        {
            Directory.CreateDirectory(_directory);
            var evidence = new SourceEvidence("commit1", "app/Service.cs", 2, 5, EvidenceKind.Static);
            new GraphSnapshotStore(_directory).Save(new GraphSnapshot("Demo", "commit1",
            [
                new("L3", GraphNodeKind.Class, "Class", "L2",
                    evidence with { Kind = EvidenceKind.Inferred }),
                new("L1", GraphNodeKind.Project, "Project", "L0", evidence),
                new("L0", GraphNodeKind.Solution, "Solution", null, evidence),
                new("L2", GraphNodeKind.Namespace, "Namespace", "L1", evidence)
            ], [], []));
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    { ["ARCHPULSE_GRAPH_STORE"] = _directory })));
            Client = _factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
