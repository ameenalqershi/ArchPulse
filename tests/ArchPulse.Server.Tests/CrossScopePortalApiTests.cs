// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class CrossScopePortalApiTests
{
    [Fact]
    public async Task ExactRevisionPortalsPreserveDirectionSourceAndExplicitUnknownAccess()
    {
        using var fixture = new Fixture();
        var first = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/PortalDemo/r1/cross-scope-portals?scopeSymbolId=module-a&pageSize=1");
        Assert.Equal(CrossScopePortalPage.ContractSchema, first.GetProperty("schema").GetString());
        Assert.Equal("r1", first.GetProperty("revision").GetString());
        Assert.Equal("PortalDemo", first.GetProperty("project").GetString());
        Assert.Equal("AuthorizationNotEvaluated", first.GetProperty("accessStatus").GetString());
        Assert.Equal("NotIndexed", first.GetProperty("featureMembershipStatus").GetString());
        Assert.Equal(2, first.GetProperty("totalCrossScopeEdges").GetInt32());
        Assert.Equal(1, first.GetProperty("outboundCount").GetInt32());
        Assert.Equal(1, first.GetProperty("inboundCount").GetInt32());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        var outbound = Assert.Single(first.GetProperty("portals").EnumerateArray());
        Assert.Equal("Outbound", outbound.GetProperty("direction").GetString());
        Assert.Equal("send", outbound.GetProperty("edgeId").GetString());
        Assert.Equal("Static", outbound.GetProperty("edge").GetProperty("evidence")
            .GetProperty("kind").GetString());
        Assert.Equal("module-b", outbound.GetProperty("externalNode")
            .GetProperty("parentSymbolId").GetString());
        Assert.Equal("send", first.GetProperty("nextAfterEdgeId").GetString());

        var next = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/PortalDemo/r1/cross-scope-portals?scopeSymbolId=module-a" +
            "&pageSize=1&afterEdgeId=send");
        var inbound = Assert.Single(next.GetProperty("portals").EnumerateArray());
        Assert.Equal("Inbound", inbound.GetProperty("direction").GetString());
        Assert.Equal("Observed", inbound.GetProperty("edge").GetProperty("evidence")
            .GetProperty("kind").GetString());
        Assert.False(next.GetProperty("hasMore").GetBoolean());
        Assert.Equal(JsonValueKind.Null, next.GetProperty("nextAfterEdgeId").ValueKind);
    }

    [Fact]
    public async Task InvalidScopingBudgetsAndCursorFailClosedWithoutMutation()
    {
        using var fixture = new Fixture();
        string url = "/api/graphs/PortalDemo/r1/cross-scope-portals";
        foreach (string suffix in new[]
        {
            "?scopeSymbolId=%20",
            "?scopeSymbolId=module-a&pageSize=0",
            "?scopeSymbolId=module-a&pageSize=101",
            "?scopeSymbolId=module-a&pageSize=NaN",
            "?scopeSymbolId=module-a&maxScanEdges=0",
            "?scopeSymbolId=module-a&maxScanEdges=1000001",
            "?scopeSymbolId=module-a&afterEdgeId=local",
            "?scopeSymbolId=module-a&afterEdgeId=unknown",
            "?scopeSymbolId=root&afterEdgeId=send"
        })
        {
            using var response = await fixture.Client.GetAsync(url + suffix);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var missing = await fixture.Client.GetAsync(url + "?scopeSymbolId=does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var absentRevision = await fixture.Client.GetAsync(
            "/api/graphs/PortalDemo/unknown/cross-scope-portals?scopeSymbolId=module-a");
        Assert.Equal(HttpStatusCode.NotFound, absentRevision.StatusCode);
        using var budget = await fixture.Client.GetAsync(
            url + "?scopeSymbolId=module-a&maxScanEdges=1");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, budget.StatusCode);
        var body = await budget.Content.ReadAsStringAsync();
        Assert.DoesNotContain(fixture.Directory, body, StringComparison.Ordinal);

        using var write = await fixture.Client.PostAsJsonAsync(
            url + "?scopeSymbolId=module-a", new { pretend = "allow" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
        using var valid = await fixture.Client.GetAsync(url + "?scopeSymbolId=module-a");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task LocalScopeExpansionEnforcesGraphBudgetsAndExplicitUnknownAccess()
    {
        using var fixture = new Fixture();
        var url = "/api/graphs/PortalDemo/r1/local-scope-expansion";
        var page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            url + "?scopeSymbolId=root&maxNodes=2&maxEdges=1&depth=3");
        Assert.Equal("archpulse.local-canonical-expansion/v1",
            page.GetProperty("schema").GetString());
        Assert.Equal("r1", page.GetProperty("revision").GetString());
        Assert.Equal("AuthorizationNotEvaluated", page.GetProperty("accessStatus").GetString());
        Assert.Equal(2, page.GetProperty("nodes").GetArrayLength());
        Assert.Single(page.GetProperty("canonicalLinks").EnumerateArray());
        Assert.True(page.GetProperty("partial").GetBoolean());
        Assert.True(page.GetProperty("nodeBudgetReached").GetBoolean());
        Assert.Equal("NoContinuationAvailable", page.GetProperty("continuationStatus").GetString());

        foreach (var invalid in new[] {
            "?scopeSymbolId=root&maxNodes=0",
            "?scopeSymbolId=root&maxEdges=2001",
            "?scopeSymbolId=root&depth=9",
            "?scopeSymbolId=root&lens=runtime",
            "?scopeSymbolId=%20" })
        {
            using var response = await fixture.Client.GetAsync(url + invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var notFound = await fixture.Client.GetAsync(url + "?scopeSymbolId=missing");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        using var wrongRevision = await fixture.Client.GetAsync(
            "/api/graphs/PortalDemo/r2/local-scope-expansion?scopeSymbolId=root");
        Assert.Equal(HttpStatusCode.NotFound, wrongRevision.StatusCode);
        using var post = await fixture.Client.PostAsJsonAsync(
            url + "?scopeSymbolId=root", new { pretend = true });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(),
            "archpulse-portal-http-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }
        public string Directory => _directory;

        public Fixture()
        {
            System.IO.Directory.CreateDirectory(_directory);
            var source = new SourceEvidence("r1", "src/Invoice.cs", 11, 3, EvidenceKind.Static);
            var observed = source with { Kind = EvidenceKind.Observed };
            new GraphSnapshotStore(_directory).Save(new GraphSnapshot("PortalDemo", "r1",
            [
                new("root", GraphNodeKind.Solution, "Main", null, source),
                new("module-a", GraphNodeKind.Project, "Accounts", "root", source),
                new("a1", GraphNodeKind.Method, "IssueInvoice", "module-a", source),
                new("a2", GraphNodeKind.Method, "CheckInvoice", "module-a", source),
                new("module-b", GraphNodeKind.Project, "Inventory", "root", source),
                new("b1", GraphNodeKind.Method, "ReserveStock", "module-b", source)
            ],
            [
                new("local", "a1", "a2", "Calls", source),
                new("send", "a1", "b1", "Calls", source),
                new("z-receive", "b1", "a1", "DependsOn", observed)
            ], []));
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    { ["ARCHPULSE_GRAPH_STORE"] = _directory })));
            Client = _factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            System.IO.Directory.Delete(_directory, recursive: true);
        }
    }
}
