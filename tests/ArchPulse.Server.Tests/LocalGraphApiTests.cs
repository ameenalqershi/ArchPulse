// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.Guardian;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchPulse.Server.Tests;

public sealed class LocalGraphApiTests
{
    [Fact]
    public async Task HealthNeverClaimsProductionReadiness()
    {
        using var fixture = new ApiFixture();
        var json = await fixture.Client.GetFromJsonAsync<JsonElement>("/health");
        Assert.Equal("local-read-only-experimental", json.GetProperty("scope").GetString());
        Assert.False(json.GetProperty("productionReady").GetBoolean());
    }

    [Fact]
    public async Task MissingRevisionIsNotInvented()
    {
        using var fixture = new ApiFixture();
        using var response = await fixture.Client.GetAsync("/api/graphs/Billing/unknown");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReadsRealPersistedGraphAndExactEvidence()
    {
        using var fixture = new ApiFixture();
        var json = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/graphs/Billing/v1");
        Assert.Equal("v1", json.GetProperty("revision").GetString());
        Assert.Equal(1, json.GetProperty("edges").GetArrayLength());
        var edge = json.GetProperty("edges")[0];
        Assert.Equal("Billing.Service/InvoiceService.cs",
            edge.GetProperty("evidence").GetProperty("relativeFile").GetString());
        Assert.Equal("Static", edge.GetProperty("evidence").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ReadsExactSymbolNeighborsAndShortPath()
    {
        using var fixture = new ApiFixture();
        const string source = "Billing.Service::M:CreateInvoice";
        const string target = "Billing.Contracts::M:Apply";
        var neighbor = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Billing/v1/neighbors?symbolId=" + Uri.EscapeDataString(source));
        Assert.Equal(1, neighbor.GetArrayLength());
        var path = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Billing/v1/path?from=" + Uri.EscapeDataString(source) +
            "&to=" + Uri.EscapeDataString(target));
        Assert.Equal(1, path.GetArrayLength());
    }

    [Fact]
    public async Task DiffUsesStoredExactRevisions()
    {
        using var fixture = new ApiFixture();
        var diff = await fixture.Client.GetFromJsonAsync<JsonElement>(
            "/api/graphs/Billing/diff?from=v1&to=v2");
        Assert.Equal("v1", diff.GetProperty("fromRevision").GetString());
        Assert.Single(diff.GetProperty("addedNodes").EnumerateArray());
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("identity")]
    [InlineData("invalid-source")]
    [InlineData("orphan")]
    public async Task CorruptPersistedGraphReturnsSanitized422ButOtherRevisionRemainsAvailable(
        string corruption)
    {
        using var fixture = new ApiFixture();
        var stored = File.ReadAllText(fixture.FirstGraphPath);
        string corrupted = corruption switch
        {
            "invalid-json" => "{private-source-marker",
            "identity" => stored.Replace("\"Project\": \"Billing\"",
                "\"Project\": \"private-source-marker\"", StringComparison.Ordinal),
            "invalid-source" => stored.Replace("\"Line\": 12", "\"Line\": -1",
                StringComparison.Ordinal),
            "orphan" => stored.Replace("\"ToSymbolId\": \"Billing.Contracts::M:Apply\"",
                "\"ToSymbolId\": \"private-source-marker\"", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        Assert.NotEqual(stored, corrupted);
        File.WriteAllText(fixture.FirstGraphPath, corrupted);

        using var broken = await fixture.Client.GetAsync("/api/graphs/Billing/v1");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, broken.StatusCode);
        var responseText = await broken.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-source-marker", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.FirstGraphPath, responseText, StringComparison.Ordinal);
        Assert.Contains("Stored graph snapshot cannot be validated", responseText,
            StringComparison.Ordinal);

        using var diff = await fixture.Client.GetAsync("/api/graphs/Billing/diff?from=v1&to=v2");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, diff.StatusCode);
        using var neighbors = await fixture.Client.GetAsync(
            "/api/graphs/Billing/v1/neighbors?symbolId=Billing.Service%3A%3AM%3ACreateInvoice");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, neighbors.StatusCode);
        using var unknown = await fixture.Client.GetAsync("/api/graphs/Billing/unknown");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var valid = await fixture.Client.GetAsync("/api/graphs/Billing/v2");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        using var health = await fixture.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task GuardianResponseIncludesWitnessFromPersistedGraph()
    {
        using var fixture = new ApiFixture();
        var request = new GuardianEvaluationRequest("Billing", "v1",
            [new ForbiddenDependencyRule("R1", "1",
                "Billing.Service", "Billing.Contracts", "Calls", RuleSeverity.Blocker)]);
        using var response = await fixture.Client.PostAsJsonAsync("/api/guardian/evaluate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var findings = await response.Content.ReadFromJsonAsync<JsonElement>();
        var finding = Assert.Single(findings.EnumerateArray());
        Assert.Equal("Violation", finding.GetProperty("outcome").GetString());
        Assert.Equal("call-1", finding.GetProperty("edgeId").GetString());
        Assert.Equal("v1", finding.GetProperty("evidence").GetProperty("revision").GetString());
    }

    [Fact]
    public async Task MutationRouteIsNotExposed()
    {
        using var fixture = new ApiFixture();
        using var response = await fixture.Client.PostAsJsonAsync("/api/graphs/Billing/v1", new { Something = true });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private sealed class ApiFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "archpulse-server-tests-" + Guid.NewGuid().ToString("N"));
        private readonly WebApplicationFactory<Program> _factory;
        public HttpClient Client { get; }
        public string FirstGraphPath { get; }

        public ApiFixture()
        {
            Directory.CreateDirectory(_directory);
            var store = new GraphSnapshotStore(_directory);
            var first = Create("v1");
            FirstGraphPath = store.Save(first);
            var second = Create("v2") with
            {
                Nodes = Create("v2").Nodes.Concat(
                [new GraphNode("Billing.Service::M:Reconcile", GraphNodeKind.Method, "Reconcile",
                    "project:Billing.Service", new SourceEvidence("v2", "Billing.Service/InvoiceService.cs",
                        15, 1, EvidenceKind.Static))]).ToArray()
            };
            store.Save(second);
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
                web.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ARCHPULSE_GRAPH_STORE"] = _directory
                    })));
            Client = _factory.CreateClient();
        }

        private static GraphSnapshot Create(string revision)
        {
            var source = new SourceEvidence(revision, "Billing.Service/InvoiceService.cs",
                12, 3, EvidenceKind.Static);
            return new GraphSnapshot("Billing", revision,
            [
                new("solution:Billing", GraphNodeKind.Solution, "Billing", null, source),
                new("project:Billing.Service", GraphNodeKind.Project,
                    "Billing.Service", "solution:Billing", source),
                new("project:Billing.Contracts", GraphNodeKind.Project,
                    "Billing.Contracts", "solution:Billing", source),
                new("Billing.Service::M:CreateInvoice", GraphNodeKind.Method,
                    "CreateInvoice", "project:Billing.Service", source),
                new("Billing.Contracts::M:Apply", GraphNodeKind.Method,
                    "Apply", "project:Billing.Contracts", source)
            ],
            [
                new("call-1", "Billing.Service::M:CreateInvoice",
                    "Billing.Contracts::M:Apply", "Calls", source)
            ], []);
        }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
