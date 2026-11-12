// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class StaticTestAssociationStoreTests
{
    private static readonly SourceEvidence CallSite =
        new("v1", "Tests/CalcTests.cs", 18, 7, EvidenceKind.Static);
    private const string Test = "Tests::M:CalcTests.Test";
    private const string Target = "Sut::M:Calc.Add";

    [Fact]
    public void PersistsExactReportImmutablyAndLoadsOnlyFromMatchingSourceRevision()
    {
        using var f = new Fixture();
        var report = f.Report();
        var path = f.Store.Save(f.Graph, report);
        Assert.EndsWith(".test-links.json", path, StringComparison.Ordinal);
        Assert.Equal(path, f.Store.Save(f.Graph, report));
        var restored = Assert.IsType<StaticTestAssociationReport>(f.Store.Load(f.Graph));
        Assert.Equal("v1", restored.GraphRevision);
        Assert.Equal(Test, Assert.Single(restored.ForTarget(Target)).TestMethodSymbolId);
        Assert.Equal(EvidenceKind.Static, restored.ForTarget(Target)[0].CallSites[0].Kind);
        Assert.Throws<InvalidOperationException>(() => f.Store.Save(f.Graph,
            report with { Diagnostics = ["changed"] }));
        Assert.Null(f.Store.Load(f.Graph with {
            Revision = "different",
            Nodes = f.Graph.Nodes.Select(n => n with { Evidence = n.Evidence with { Revision = "different" } }).ToArray(),
            Edges = f.Graph.Edges.Select(e => e with { Evidence = e.Evidence with { Revision = "different" } }).ToArray()
        }));
    }

    [Fact]
    public void FalseObservedClaimsAndForgedSourceSitesAreRejected()
    {
        using var f = new Fixture();
        var report = f.Report();
        var link = Assert.Single(report.Associations);
        Assert.Throws<InvalidDataException>(() => f.Store.Save(f.Graph,
            report with { Associations = [link with { ExecutionEvidence = "Observed" }] }));
        Assert.Throws<InvalidDataException>(() => f.Store.Save(f.Graph,
            report with { Associations = [link with {
                CallSites = [CallSite with { Line = 999 }]
            }] }));
        Assert.Throws<InvalidDataException>(() => f.Store.Save(f.Graph,
            report with { GraphRevision = "other" }));
    }

    [Fact]
    public void DuplicateAndDanglingLinksAreRejected()
    {
        using var f = new Fixture();
        var report = f.Report();
        var link = Assert.Single(report.Associations);
        Assert.Throws<InvalidDataException>(() => f.Store.Save(f.Graph,
            report with { Associations = [link, link] }));
        Assert.Throws<InvalidDataException>(() => f.Store.Save(f.Graph,
            report with { Associations = [link with { TargetSymbolId = "missing" }] }));
        Assert.Throws<InvalidDataException>(() => f.Store.Save(f.Graph,
            report with { Associations = [link with { CallSites = [CallSite, CallSite] }] }));
    }

    [Fact]
    public void CorruptedSerializedSidecarFailsClosed()
    {
        using var f = new Fixture();
        var path = f.Store.Save(f.Graph, f.Report());
        File.WriteAllText(path, "{}");
        Assert.Throws<InvalidDataException>(() => f.Store.Load(f.Graph));
        File.WriteAllText(path, "{wrong-json");
        Assert.Throws<JsonException>(() => f.Store.Load(f.Graph));
    }

    private sealed class Fixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-association-store-" + Guid.NewGuid().ToString("N"));
        public StaticTestAssociationStore Store { get; }
        public GraphSnapshot Graph { get; }

        public Fixture()
        {
            Store = new StaticTestAssociationStore(DirectoryPath);
            Graph = new GraphSnapshot("Sample", "v1",
            [
                new("project:Tests", GraphNodeKind.Project, "Tests", null, CallSite),
                new("project:Sut", GraphNodeKind.Project, "Sut", null, CallSite),
                new(Test, GraphNodeKind.Method, "Test", "project:Tests", CallSite),
                new(Target, GraphNodeKind.Method, "Add", "project:Sut", CallSite)
            ], [new("call-1", Test, Target, "Calls", CallSite)], []);
        }

        public StaticTestAssociationReport Report() => new(
            "archpulse.method-test-associations/v1", Graph.Project, Graph.Revision,
            "RoslynStaticDirectOnly", "NotRunnerDiscovered", "NoRunOrCoverageReceipts",
            [new(Test, "Tests", "xUnit", CallSite, "KnownAttributeNameUnverifiedByRunner",
                "TestMethodDeclarationNotParameterizedCase", "NotRun")],
            [new(Test, Target, "Tests", "xUnit",
                "DirectInvocation", "StaticSourceOnly", "Unobserved", "Unknown", [CallSite])],
            []);

        public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
    }
}
