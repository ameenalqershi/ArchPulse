// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.McpServer;
using Xunit;

namespace ArchPulse.McpServer.Tests;

public sealed class LocalDataJourneyMcpTests
{
    [Fact]
    public void ReadsMetadataOnlyAndDoesNotClaimSanitizerApproval()
    {
        using var f = new Fixture(true);
        var result = GraphTools.LocalDataJourney(f.Graphs, f.Journeys, "Corpus", "rev");
        Assert.NotEqual(true, result.IsError);
        var report = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(LocalDataJourneyContract.Schema, report.GetProperty("schema").GetString());
        Assert.Equal(LocalDataJourneyContract.Scope, report.GetProperty("scope").GetString());
        Assert.Equal("Unknown", report.GetProperty("runtimeStatus").GetString());
        Assert.Equal("DeclaredSanitizerNeedsReview",
            report.GetProperty("findings")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public void MissingIsUnknownRatherThanAbsenceOfSensitiveFlows()
    {
        using var f = new Fixture(false);
        var result = GraphTools.LocalDataJourney(f.Graphs, f.Journeys, "Corpus", "rev");
        Assert.NotEqual(true, result.IsError);
        Assert.Equal("NotIndexed",
            Assert.IsType<JsonElement>(result.StructuredContent)
                .GetProperty("journeyStatus").GetString());
        Assert.True(GraphTools.LocalDataJourney(f.Graphs, f.Journeys,
            "Corpus", "unindexed").IsError);
    }

    [Fact]
    public void ForgedEvidenceFailsClosed()
    {
        using var f = new Fixture(true);
        var file = Assert.Single(Directory.GetFiles(f.Directory, "*.datajourney.json"));
        File.WriteAllText(file, File.ReadAllText(file)
            .Replace("DeclaredSanitizerNeedsReview", "SecurityVerified"));
        Assert.True(GraphTools.LocalDataJourney(f.Graphs, f.Journeys,
            "Corpus", "rev").IsError);
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-dj-mcp-" + Guid.NewGuid().ToString("N"));
        public GraphSnapshotStore Graphs { get; }
        public LocalDataJourneyStore Journeys { get; }
        public Fixture(bool present)
        {
            var ev = new SourceEvidence("rev", "Flow.cs", 6, 1, EvidenceKind.Static);
            var method = "Corpus::M:Example.Flows.Sanitized(System.String)";
            Graphs = new GraphSnapshotStore(Directory);
            Journeys = new LocalDataJourneyStore(Directory);
            var graph = new GraphSnapshot("Corpus", "rev",
                [new("project:Corpus", GraphNodeKind.Project, "Corpus", null, ev),
                 new(method, GraphNodeKind.Method, "Sanitized", "project:Corpus", ev)], [], []);
            Graphs.Save(graph);
            if (present)
            {
                var report = new LocalDataJourneyReport(LocalDataJourneyContract.Schema,
                    graph.Project, graph.Revision, LocalDataJourneyContract.Scope,
                    LocalDataJourneyContract.RuntimeStatus,
                    [new LocalDataFlowFinding(method, "annotated-sink:M:Foo.Sink",
                        LocalFlowDestination.DeclaredSink,
                        LocalFlowOutcome.DeclaredSanitizerNeedsReview,
                        method + "::parameter:secret", ev)], []);
                Journeys.Save(graph, report);
            }
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
