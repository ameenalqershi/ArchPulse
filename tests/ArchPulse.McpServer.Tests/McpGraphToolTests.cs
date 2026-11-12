// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.McpServer;
using ModelContextProtocol.Protocol;
using Xunit;

namespace ArchPulse.McpServer.Tests;

public sealed class McpGraphToolTests
{
    [Fact]
    public void OpeningExactRevisionReturnsStructuredGraphAndProvenance()
    {
        using var fixture = new StoreFixture();
        var response = GraphTools.OpenGraph(fixture.Store, "Billing", "golden-mcp");
        Assert.NotEqual(true, response.IsError);
        var json = Assert.IsType<JsonElement>(response.StructuredContent);
        Assert.Equal("Billing", json.GetProperty("project").GetString());
        Assert.Equal(5, json.GetProperty("nodes").GetArrayLength());
        Assert.Equal("Static", json.GetProperty("edges")[0]
            .GetProperty("evidence").GetProperty("kind").GetString());
        Assert.Equal("golden-mcp", json.GetProperty("revision").GetString());
    }

    [Fact]
    public void MissingRevisionReturnsExplicitErrorNotInventedGraph()
    {
        using var fixture = new StoreFixture();
        var response = GraphTools.Snapshot(fixture.Store, "Billing", "not-stored");
        Assert.True(response.IsError);
        Assert.Null(response.StructuredContent);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(response.Content));
        Assert.Contains("No stored graph", text.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void SymbolContextIncludesExactSourceLocationAndNeighbor()
    {
        using var fixture = new StoreFixture();
        var response = GraphTools.SymbolContext(fixture.Store, "Billing",
            "golden-mcp", "Billing.Service::M:Create");
        var json = Assert.IsType<JsonElement>(response.StructuredContent);
        Assert.Equal("Billing.Service::M:Create",
            json.GetProperty("symbol").GetProperty("symbolId").GetString());
        Assert.Equal("InvoiceService.cs",
            json.GetProperty("symbol").GetProperty("evidence").GetProperty("relativeFile").GetString());
        Assert.Single(json.GetProperty("edges").EnumerateArray());
    }

    [Fact]
    public void FindsDirectedStaticPathWithoutClaimingRuntimeTrace()
    {
        using var fixture = new StoreFixture();
        var response = GraphTools.Path(fixture.Store, "Billing", "golden-mcp",
            "Billing.Service::M:Create", "Billing.Contracts::M:Apply");
        var data = Assert.IsType<JsonElement>(response.StructuredContent);
        Assert.Equal(1, data.GetProperty("edges").GetArrayLength());
        Assert.Contains("not-a-runtime-trace",
            data.GetProperty("evidenceScope").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void GuardianCheckHasNoPersistenceSideEffects()
    {
        using var fixture = new StoreFixture();
        var response = GraphTools.GuardianCheck(fixture.Store, "Billing", "golden-mcp",
            "Billing.Service", "Billing.Contracts", "Calls");
        var data = Assert.IsType<JsonElement>(response.StructuredContent);
        Assert.Equal("Violation", data.GetProperty("results")[0].GetProperty("outcome").GetString());
        Assert.Single(Directory.EnumerateFiles(fixture.Path, "*.graph.json"));
    }

    [Fact]
    public void UiResourceDeclaresRealHostBridgeAndSourceOnlyRules()
    {
        var html = GraphUiResource.GraphUi();
        Assert.Contains("ui/initialize", html, StringComparison.Ordinal);
        Assert.Contains("ui/notifications/tool-result", html, StringComparison.Ordinal);
        Assert.Contains("archpulse_graph_snapshot", html, StringComparison.Ordinal);
        Assert.Contains("parentSymbolId", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://cdn.", html, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StoreFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "archpulse-mcp-unit-" + Guid.NewGuid().ToString("N"));
        public GraphSnapshotStore Store { get; }
        public StoreFixture()
        {
            Directory.CreateDirectory(Path);
            Store = new GraphSnapshotStore(Path);
            var ev = new SourceEvidence("golden-mcp", "InvoiceService.cs", 9, 4, EvidenceKind.Static);
            Store.Save(new GraphSnapshot("Billing", "golden-mcp",
            [
                new("solution:Billing", GraphNodeKind.Solution, "Billing", null, ev),
                new("project:Billing.Service", GraphNodeKind.Project,
                    "Billing.Service", "solution:Billing", ev),
                new("project:Billing.Contracts", GraphNodeKind.Project,
                    "Billing.Contracts", "solution:Billing", ev),
                new("Billing.Service::M:Create", GraphNodeKind.Method,
                    "Create", "project:Billing.Service", ev),
                new("Billing.Contracts::M:Apply", GraphNodeKind.Method,
                    "Apply", "project:Billing.Contracts", ev)
            ],
            [
                new("call", "Billing.Service::M:Create",
                    "Billing.Contracts::M:Apply", "Calls", ev)
            ], []));
        }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
