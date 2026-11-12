// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.McpServer;
using Xunit;

namespace ArchPulse.McpServer.Tests;

public sealed class McpStaticTestLinksTests
{
    [Fact]
    public void MissingSidecarIsNotIndexedNotEvidenceOfNoTests()
    {
        using var f = new Fixture(false);
        var result = GraphTools.LinkedTests(f.Graph, f.Links, "Billing", "v1", Fixture.Target);
        Assert.NotEqual(true, result.IsError);
        var json = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("NotIndexed", json.GetProperty("mappingStatus").GetString());
        Assert.Equal(0, json.GetProperty("links").GetArrayLength());
        Assert.Equal("Unknown", json.GetProperty("executionStatus").GetString());
    }

    [Fact]
    public void ExactSymbolHasOnlySourceEvidencedRelation()
    {
        using var f = new Fixture(true);
        var result = GraphTools.LinkedTests(f.Graph, f.Links, "Billing", "v1", Fixture.Target);
        var json = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("MappedDirectStatic", json.GetProperty("mappingStatus").GetString());
        Assert.Equal("StaticSourceOnly", json.GetProperty("links")[0]
            .GetProperty("verification").GetString());
        Assert.False(json.GetProperty("observedCoverage").GetBoolean());
        Assert.Equal("Unobserved", json.GetProperty("links")[0]
            .GetProperty("executionEvidence").GetString());
    }

    [Fact]
    public void UnknownRevisionAndUnknownSymbolAreErrors()
    {
        using var f = new Fixture(true);
        Assert.True(GraphTools.LinkedTests(f.Graph, f.Links, "Billing", "missing", Fixture.Target).IsError);
        Assert.True(GraphTools.LinkedTests(f.Graph, f.Links, "Billing", "v1", "not-a-method").IsError);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Test = "Billing.Tests::M:Fixture.Test";
        public const string Target = "Billing.Service::M:Service.Run";
        private readonly string _dir = Path.Combine(Path.GetTempPath(),
            "archpulse-mcp-testlinks-" + Guid.NewGuid().ToString("N"));
        public GraphSnapshotStore Graph { get; }
        public StaticTestAssociationStore Links { get; }
        public Fixture(bool withReport)
        {
            var ev = new SourceEvidence("v1", "Tests/Fixture.cs", 7, 3, EvidenceKind.Static);
            Graph = new GraphSnapshotStore(_dir);
            Links = new StaticTestAssociationStore(_dir);
            var graph = new GraphSnapshot("Billing", "v1",
            [
                new("solution:Billing", GraphNodeKind.Solution, "Billing", null, ev),
                new("project:Billing.Tests", GraphNodeKind.Project,
                    "Billing.Tests", "solution:Billing", ev),
                new("project:Billing.Service", GraphNodeKind.Project,
                    "Billing.Service", "solution:Billing", ev),
                new(Test, GraphNodeKind.Method, "Test", "project:Billing.Tests", ev),
                new(Target, GraphNodeKind.Method, "Run", "project:Billing.Service", ev)
            ], [new("call", Test, Target, "Calls", ev)], []);
            Graph.Save(graph);
            if (withReport)
                Links.Save(graph, new StaticTestAssociationReport(
                    "archpulse.method-test-associations/v1", "Billing", "v1",
                    "RoslynStaticDirectOnly", "NotRunnerDiscovered",
                    "NoRunOrCoverageReceipts",
                    [new(Test, "Billing.Tests", "xUnit", ev,
                        "KnownAttributeNameUnverifiedByRunner",
                        "TestMethodDeclarationNotParameterizedCase", "NotRun")],
                    [new(Test, Target, "Billing.Tests", "xUnit",
                        "DirectInvocation", "StaticSourceOnly", "Unobserved", "Unknown", [ev])], []));
        }
        public void Dispose() => Directory.Delete(_dir, recursive: true);
    }
}
