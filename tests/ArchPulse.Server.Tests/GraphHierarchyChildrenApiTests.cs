// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class GraphHierarchyChildrenApiTests
{
    [Fact]
    public async Task ExactRootAndParentPagesExposeSourceBackedCanonicalHierarchy()
    {
        using var fixture = new Fixture();
        var root = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Hierarchy/r1/children-page?pageSize=1");
        Assert.Equal(GraphChildPage.ContractSchema, root.GetProperty("schema").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parentSymbolId").ValueKind);
        Assert.Equal("Hierarchy", root.GetProperty("project").GetString());
        Assert.Equal("r1", root.GetProperty("revision").GetString());
        Assert.Equal("root", Assert.Single(root.GetProperty("nodes").EnumerateArray())
            .GetProperty("symbolId").GetString());
        Assert.False(root.GetProperty("hasMore").GetBoolean());

        var first = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Hierarchy/r1/children-page?parentSymbolId=root&pageSize=1");
        Assert.Equal("root", first.GetProperty("parentSymbolId").GetString());
        Assert.Equal("child:a", Assert.Single(first.GetProperty("nodes").EnumerateArray())
            .GetProperty("symbolId").GetString());
        Assert.Equal("Static", first.GetProperty("nodes")[0].GetProperty("evidence")
            .GetProperty("kind").GetString());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal("child:a", first.GetProperty("nextAfterSymbolId").GetString());

        var second = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Hierarchy/r1/children-page?parentSymbolId=root&pageSize=1" +
            "&afterSymbolId=child%3Aa");
        Assert.Equal("child:b", Assert.Single(second.GetProperty("nodes").EnumerateArray())
            .GetProperty("symbolId").GetString());
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextAfterSymbolId").ValueKind);

        var leaf = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Hierarchy/r1/children-page?parentSymbolId=child%3Ab");
        Assert.Empty(leaf.GetProperty("nodes").EnumerateArray());
    }

    [Fact]
    public async Task InvalidOrForeignCursorsAndUnknownScopeFailClosed()
    {
        using var fixture = new Fixture();
        string baseUrl = "/api/graphs/Hierarchy/r1/children-page";
        foreach (var suffix in new[]
        {
            "?parentSymbolId=root&afterSymbolId=missing",
            "?parentSymbolId=root&afterSymbolId=grandchild",
            "?parentSymbolId=root&pageSize=0",
            "?parentSymbolId=root&pageSize=101",
            "?parentSymbolId=root&pageSize=NaN",
            "?parentSymbolId=root&afterSymbolId=%20",
            "?parentSymbolId=%20",
            "?afterSymbolId=child%3Aa"
        })
        {
            using var invalid = await fixture.Client.GetAsync(baseUrl + suffix);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var unknownParent = await fixture.Client.GetAsync(
            baseUrl + "?parentSymbolId=missing");
        Assert.Equal(HttpStatusCode.NotFound, unknownParent.StatusCode);
        using var unknownRevision = await fixture.Client.GetAsync(
            "/api/graphs/Hierarchy/unknown/children-page");
        Assert.Equal(HttpStatusCode.NotFound, unknownRevision.StatusCode);

        // The new read-only route cannot mutate the underlying graph.
        using var write = await fixture.Client.PostAsJsonAsync(baseUrl, new { data = 1 });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
        using var stillValid = await fixture.Client.GetAsync(baseUrl);
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(),
            "archpulse-hierarchy-api-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }

        public Fixture()
        {
            Directory.CreateDirectory(_directory);
            var evidence = new SourceEvidence("r1", "src/Foo.cs", 2, 3, EvidenceKind.Static);
            new GraphSnapshotStore(_directory).Save(new GraphSnapshot("Hierarchy", "r1",
            [
                new("grandchild", GraphNodeKind.Method, "Method", "child:a", evidence),
                new("child:b", GraphNodeKind.Class, "B", "root", evidence),
                new("child:a", GraphNodeKind.Class, "A", "root", evidence),
                new("root", GraphNodeKind.Solution, "Root", null, evidence)
            ], [], []));
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ARCHPULSE_GRAPH_STORE"] = _directory
                    })));
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
