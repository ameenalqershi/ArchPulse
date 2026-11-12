// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.McpServer;
using Xunit;

namespace ArchPulse.McpServer.Tests;

public sealed class McpFeatureImpactTests
{
    [Fact]
    public void ExactCatalogAndGraphRevisionReturnOnlyCandidateImpacts()
    {
        using var f = new Fixture(true);
        var response = GraphTools.FeatureImpact(f.Graphs, f.Features, "Billing", "old", "new");
        Assert.NotEqual(true, response.IsError);
        var json = Assert.IsType<JsonElement>(response.StructuredContent);
        Assert.Equal("Unknown", json.GetProperty("runtimeStatus").GetString());
        Assert.Equal(2, json.GetProperty("impacts").GetArrayLength());
        Assert.Equal(FeatureImpactAnalyzer.Scope, json.GetProperty("evidenceScope").GetString());
        Assert.All(json.GetProperty("impacts").EnumerateArray(), item =>
            Assert.Equal("create-invoice", item.GetProperty("featureId").GetString()));
    }

    [Fact]
    public void MissingCatalogIsNotIndexedNotEvidenceOfNoEffect()
    {
        using var f = new Fixture(false);
        var response = GraphTools.FeatureImpact(f.Graphs, f.Features, "Billing", "old", "new");
        Assert.NotEqual(true, response.IsError);
        var json = Assert.IsType<JsonElement>(response.StructuredContent);
        Assert.Equal("NotIndexed", json.GetProperty("catalogStatus").GetString());
        Assert.Equal("Unknown", json.GetProperty("runtimeStatus").GetString());
        Assert.Equal(0, json.GetProperty("impacts").GetArrayLength());
    }

    [Fact]
    public void UnknownRevisionOrForgedCatalogNeverReturnsAClaim()
    {
        using var f = new Fixture(true);
        Assert.True(GraphTools.FeatureImpact(f.Graphs, f.Features,
            "Billing", "old", "not-indexed").IsError);
        var file = Assert.Single(Directory.GetFiles(f.Directory, "*.features.json"));
        File.WriteAllText(file, File.ReadAllText(file)
            .Replace("method:Entry", "method:Fake", StringComparison.Ordinal));
        Assert.True(GraphTools.FeatureImpact(f.Graphs, f.Features, "Billing", "old", "new").IsError);
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-impact-mcp-" + Guid.NewGuid().ToString("N"));
        public GraphSnapshotStore Graphs { get; }
        public FeatureCatalogStore Features { get; }

        public Fixture(bool withCatalog)
        {
            var evOld = new SourceEvidence("old", "Billing.cs", 2, 1, EvidenceKind.Static);
            var evNew = evOld with { Revision = "new" };
            Graphs = new GraphSnapshotStore(Directory);
            Features = new FeatureCatalogStore(Directory);
            var old = new GraphSnapshot("Billing", "old",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, evOld),
                new("method:Entry", GraphNodeKind.Method, "Entry", "project:Billing", evOld)
            ], [], []);
            var updated = new GraphSnapshot("Billing", "new",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, evNew),
                new("method:Entry", GraphNodeKind.Method, "Entry", "project:Billing", evNew),
                new("method:Changed", GraphNodeKind.Method, "Changed", "project:Billing", evNew)
            ], [new("new-call", "method:Entry", "method:Changed", "Calls", evNew)], []);
            Graphs.Save(old);
            Graphs.Save(updated);
            if (withCatalog)
                Features.Save(old, new FeatureCatalog(FeatureAtlas.Schema, "Billing", "old",
                [
                    new BusinessFeature("create-invoice", "Create Invoice",
                        "Manually inferred business intent", "Billing", FeatureCriticality.P0,
                        ["Create"], ["Review"], [new("method:Entry", "Operator proposed",
                            EvidenceKind.Inferred)])
                ]));
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
