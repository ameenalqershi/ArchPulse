// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class LocalGraphTestLinksApiTests
{
    private const string Project = "Billing";
    private const string Revision = "v1";
    private const string TestId = "Billing.Tests::M:CalculatorTests.Test";
    private const string TargetId = "Billing.Service::M:Calculator.Add";
    private const string SecondId = "Billing.Service::M:Calculator.Unmapped";
    private static readonly SourceEvidence Evidence =
        new(Revision, "Tests/CalculatorTests.cs", 9, 5, EvidenceKind.Static);

    [Fact]
    public async Task MissingReportNeverClaimsNoTestsMapped()
    {
        using var f = new Fixture(withReport: false);
        var body = await f.Get(TargetId);
        Assert.Equal("NotIndexed", body.GetProperty("mappingStatus").GetString());
        Assert.Equal(0, body.GetProperty("links").GetArrayLength());
        Assert.False(body.GetProperty("observedCoverage").GetBoolean());
        Assert.Equal("Unknown", body.GetProperty("executionStatus").GetString());
    }

    [Fact]
    public async Task ExactMethodReturnsOnlyStaticSourceBackedLinks()
    {
        using var f = new Fixture(withReport: true);
        var body = await f.Get(TargetId);
        Assert.Equal(Revision, body.GetProperty("revision").GetString());
        Assert.Equal(TargetId, body.GetProperty("targetSymbolId").GetString());
        Assert.Equal("MappedDirectStatic", body.GetProperty("mappingStatus").GetString());
        var link = Assert.Single(body.GetProperty("links").EnumerateArray());
        Assert.Equal(TestId, link.GetProperty("testMethodSymbolId").GetString());
        Assert.Equal("DirectInvocation", link.GetProperty("relation").GetString());
        Assert.Equal("StaticSourceOnly", link.GetProperty("verification").GetString());
        Assert.Equal("Unobserved", link.GetProperty("executionEvidence").GetString());
        Assert.Equal("Unknown", link.GetProperty("lastResult").GetString());
        Assert.Equal(9, link.GetProperty("callSites")[0].GetProperty("line").GetInt32());
    }

    [Fact]
    public async Task IndexedButUnlinkedMethodHasUnmappedStatusNotZeroCoverage()
    {
        using var f = new Fixture(withReport: true);
        var body = await f.Get(SecondId);
        Assert.Equal("Unmapped", body.GetProperty("mappingStatus").GetString());
        Assert.False(body.GetProperty("observedCoverage").GetBoolean());
        Assert.Equal(0, body.GetProperty("links").GetArrayLength());
    }

    [Fact]
    public async Task UnknownSymbolOrRevisionIsNotInventedAndWritesRemainDisabled()
    {
        using var f = new Fixture(withReport: true);
        using var bad = await f.Client.GetAsync(Path("unknown", TargetId));
        Assert.Equal(HttpStatusCode.NotFound, bad.StatusCode);
        using var other = await f.Client.GetAsync(Path(Revision, "missing-id"));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var post = await f.Client.PostAsJsonAsync(Path(Revision, TargetId), new { action = "run" });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task EmptyOrMissingSymbolIdRejectedRatherThanGuessed()
    {
        using var f = new Fixture(withReport: true);
        using var bad = await f.Client.GetAsync("/api/graphs/Billing/v1/test-links");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using var empty = await f.Client.GetAsync("/api/graphs/Billing/v1/test-links?symbolId=");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    private static string Path(string revision, string symbol) =>
        "/api/graphs/" + Project + "/" + revision + "/test-links?symbolId=" +
        Uri.EscapeDataString(symbol);

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-test-links-api-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }

        public Fixture(bool withReport)
        {
            var graph = CreateGraph();
            new GraphSnapshotStore(_root).Save(graph);
            if (withReport)
                new StaticTestAssociationStore(_root).Save(graph, CreateReport());
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    { ["ARCHPULSE_GRAPH_STORE"] = _root })));
            Client = _factory.CreateClient();
        }

        public async Task<JsonElement> Get(string symbol) =>
            await Client.GetFromJsonAsync<JsonElement>(Path(Revision, symbol));

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            Directory.Delete(_root, recursive: true);
        }

        private static GraphSnapshot CreateGraph() => new(Project, Revision,
        [
            new("solution:Billing", GraphNodeKind.Solution, "Billing", null, Evidence),
            new("project:Billing.Tests", GraphNodeKind.Project, "Billing.Tests", "solution:Billing", Evidence),
            new("project:Billing.Service", GraphNodeKind.Project, "Billing.Service", "solution:Billing", Evidence),
            new(TestId, GraphNodeKind.Method, "Test", "project:Billing.Tests", Evidence),
            new(TargetId, GraphNodeKind.Method, "Add", "project:Billing.Service", Evidence),
            new(SecondId, GraphNodeKind.Method, "Unmapped", "project:Billing.Service", Evidence)
        ],
        [
            new("direct-1", TestId, TargetId, "Calls", Evidence)
        ], []);

        private static StaticTestAssociationReport CreateReport() =>
            new("archpulse.method-test-associations/v1", Project, Revision,
                "RoslynStaticDirectOnly", "NotRunnerDiscovered", "NoRunOrCoverageReceipts",
                [new(TestId, "Billing.Tests", "xUnit", Evidence,
                    "KnownAttributeNameUnverifiedByRunner",
                    "TestMethodDeclarationNotParameterizedCase", "NotRun")],
                [new(TestId, TargetId, "Billing.Tests", "xUnit", "DirectInvocation",
                    "StaticSourceOnly", "Unobserved", "Unknown", [Evidence])],
                []);
    }
}
