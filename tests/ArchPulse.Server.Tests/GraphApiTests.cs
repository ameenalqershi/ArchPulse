// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using ArchPulse.Core;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class GraphApiTests : IDisposable
{
    private const string Sha = "cccccccccccccccccccccccccccccccccccccccc";
    private readonly string _file;
    private readonly WebApplicationFactory<Program> _factory;

    public GraphApiTests()
    {
        _file = Path.Combine(Path.GetTempPath(), "archpulse-api-" + Guid.NewGuid().ToString("N") + ".json");
        var graph = new GraphBuilder(Sha);
        var evidence = new GraphProvenance(Sha, EvidenceKind.Static, "fixture", "T:Example.Invoice", null);
        graph.AddNode(new GraphNode("n123", GraphNodeKind.Class, "Invoice", evidence));
        File.WriteAllText(_file, JsonSerializer.Serialize(graph.Build()));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ARCHPULSE_GRAPH_PATH"] = _file })));
    }

    [Fact]
    public async Task GraphResponseContainsRealSnapshotIdentity()
    {
        var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/graph");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(Sha, doc.RootElement.GetProperty("revision").GetString());
        Assert.Equal("n123", doc.RootElement.GetProperty("nodes")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task SelectionIsByNodeIdentityNotAmbiguousText()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/graph/nodes/Invoice")).StatusCode);
        var response = await client.GetAsync("/api/v1/graph/nodes/n123");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"revision\":\"" + Sha + "\"", body);
        Assert.Contains("\"id\":\"n123\"", body);
    }

    [Fact]
    public async Task MermaidIsGeneratedFromActualNodes()
    {
        var text = await _factory.CreateClient().GetStringAsync("/api/v1/graph/mermaid");
        Assert.StartsWith("flowchart LR", text);
        Assert.Contains("n123[\"Invoice\"]", text);
    }

    public void Dispose()
    {
        _factory.Dispose();
        File.Delete(_file);
    }
}
