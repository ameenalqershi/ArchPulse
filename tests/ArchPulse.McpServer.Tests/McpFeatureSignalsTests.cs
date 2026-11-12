// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.McpServer;
using Xunit;

namespace ArchPulse.McpServer.Tests;

public sealed class McpFeatureSignalsTests
{
    [Fact]
    public void MissingSidecarIsNotIndexedNotAClaimOfAbsence()
    {
        using var f = new Fixture(false);
        var result = GraphTools.FeatureSignals(f.Graphs, f.Discoveries, "Billing", "rev");
        Assert.NotEqual(true, result.IsError);
        var body = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("NotIndexed", body.GetProperty("discoveryStatus").GetString());
        Assert.Empty(body.GetProperty("signals").EnumerateArray());
    }

    [Fact]
    public void ExistingSignalsPreserveInferredBusinessMeaningAndExactStaticSource()
    {
        using var f = new Fixture(true);
        var result = GraphTools.FeatureSignals(f.Graphs, f.Discoveries, "Billing", "rev");
        Assert.NotEqual(true, result.IsError);
        var body = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("IndexedCandidates", body.GetProperty("discoveryStatus").GetString());
        Assert.Equal("Unknown", body.GetProperty("runtimeStatus").GetString());
        var signal = Assert.Single(body.GetProperty("signals").EnumerateArray());
        Assert.Equal("Inferred", signal.GetProperty("businessClaim").GetString());
        Assert.Equal("Static", signal.GetProperty("source").GetProperty("kind").GetString());
        Assert.Equal("NamedHandler", signal.GetProperty("kind").GetString());
    }

    [Fact]
    public void UnknownGraphOrForgedSignalFailsClosed()
    {
        using var f = new Fixture(true);
        Assert.True(GraphTools.FeatureSignals(f.Graphs, f.Discoveries,
            "Billing", "wrong-rev").IsError);
        var path = Assert.Single(Directory.GetFiles(f.Directory, "*.feature-signals.json"));
        File.WriteAllText(path, File.ReadAllText(path).Replace("feature-signal:", "forged:"));
        Assert.True(GraphTools.FeatureSignals(f.Graphs, f.Discoveries,
            "Billing", "rev").IsError);
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-signal-mcp-" + Guid.NewGuid().ToString("N"));
        public GraphSnapshotStore Graphs { get; }
        public FeatureDiscoveryStore Discoveries { get; }

        public Fixture(bool withReport)
        {
            var evidence = new SourceEvidence("rev", "Handler.cs", 3, 1, EvidenceKind.Static);
            Graphs = new GraphSnapshotStore(Directory);
            Discoveries = new FeatureDiscoveryStore(Directory);
            var graph = new GraphSnapshot("Billing", "rev",
            [
                new("project:Billing", GraphNodeKind.Project, "Billing", null, evidence),
                new("Billing::M:ReserveInventoryHandler.Handle", GraphNodeKind.Method,
                    "Handle()", "project:Billing", evidence)
            ], [], []);
            Graphs.Save(graph);
            if (withReport)
                Discoveries.Save(graph, new FeatureDiscoveryReport(
                    FeatureDiscoveryContract.Schema, "Billing", "rev",
                    FeatureDiscoveryContract.Scope, "Unknown",
                    [
                        new FeatureCandidateSignal(
                            "feature-signal:NamedHandler:Billing::M:ReserveInventoryHandler.Handle",
                            FeatureSignalKind.NamedHandler,
                            "Billing::M:ReserveInventoryHandler.Handle",
                            "reserve-inventory",
                            "HandlerTypeAndMethodNaming;NotRuntimeRegistration",
                            "Name-only candidate, no DI registration.",
                            evidence, EvidenceKind.Inferred, null, null)
                    ], []));
        }

        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
