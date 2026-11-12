// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.McpServer;
using Xunit;

namespace ArchPulse.McpServer.Tests;

public sealed class McpFeatureAtlasTests
{
    [Fact]
    public void LocalToolReturnsExplicitCandidateMembershipAndRealGraphEdge()
    {
        using var f = new Fixture(withCatalog: true);
        var response = GraphTools.FeatureScope(f.Graphs, f.Features,
            "Billing", "rev", "create-invoice");
        Assert.NotEqual(true, response.IsError);
        var json = Assert.IsType<JsonElement>(response.StructuredContent);
        Assert.Equal("create-invoice", json.GetProperty("feature").GetProperty("id").GetString());
        Assert.Equal("Designed", json.GetProperty("feature").GetProperty("members")[0]
            .GetProperty("claim").GetString());
        Assert.Equal("Static", json.GetProperty("staticEdges")[0]
            .GetProperty("evidence").GetProperty("kind").GetString());
        Assert.Equal("Unknown", json.GetProperty("executionStatus").GetString());
        Assert.False(json.GetProperty("observedCoverage").GetBoolean());
    }

    [Fact]
    public void MissingCatalogUnknownFeatureAndWrongRevisionAreExplicitErrors()
    {
        using var f = new Fixture(withCatalog: false);
        Assert.True(GraphTools.FeatureScope(f.Graphs, f.Features,
            "Billing", "rev", "create-invoice").IsError);
        using var valid = new Fixture(withCatalog: true);
        Assert.True(GraphTools.FeatureScope(valid.Graphs, valid.Features,
            "Billing", "rev", "refund").IsError);
        Assert.True(GraphTools.FeatureScope(valid.Graphs, valid.Features,
            "Billing", "other-rev", "create-invoice").IsError);
    }

    [Fact]
    public void CorruptedCatalogCannotBecomeAnMcpAssertion()
    {
        using var f = new Fixture(withCatalog: true);
        var file = Assert.Single(Directory.GetFiles(f.Directory, "*.features.json"));
        File.WriteAllText(file, File.ReadAllText(file)
            .Replace("method:Create", "method:Forged", StringComparison.Ordinal));
        Assert.True(GraphTools.FeatureScope(f.Graphs, f.Features,
            "Billing", "rev", "create-invoice").IsError);
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-mcp-feature-" + Guid.NewGuid().ToString("N"));
        public GraphSnapshotStore Graphs { get; }
        public FeatureCatalogStore Features { get; }
        public Fixture(bool withCatalog)
        {
            var ev = new SourceEvidence("rev", "Billing.cs", 5, 1, EvidenceKind.Static);
            Graphs = new GraphSnapshotStore(Directory);
            Features = new FeatureCatalogStore(Directory);
            var graph = new GraphSnapshot("Billing", "rev",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, ev),
                new("method:Create", GraphNodeKind.Method, "Create", "project:Billing", ev),
                new("method:Save", GraphNodeKind.Method, "Save", "project:Billing", ev)
            ], [new("call", "method:Create", "method:Save", "Calls", ev)], []);
            Graphs.Save(graph);
            if (withCatalog)
                Features.Save(graph, new FeatureCatalog(FeatureAtlas.Schema, "Billing", "rev",
                [
                    new BusinessFeature("create-invoice", "Create Invoice", "Proposed",
                        "Billing", FeatureCriticality.P1, ["Invoice request"],
                        ["Review scenario"],
                        [
                            new("method:Create", "Candidate handler", EvidenceKind.Designed),
                            new("method:Save", "Candidate persistence", EvidenceKind.Inferred)
                        ])
                ]));
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
