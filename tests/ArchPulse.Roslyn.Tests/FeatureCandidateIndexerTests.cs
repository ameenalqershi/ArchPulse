// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class FeatureCandidateIndexerTests
{
    private static string FixtureSolution
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchPulse.sln")))
                directory = directory.Parent;
            return Path.Combine(directory?.FullName ??
                throw new InvalidOperationException("Cannot find trusted ArchPulse checkout."),
                "examples", "feature-signals", "FeatureSignals.sln");
        }
    }

    [Fact]
    public async Task DiscoversInvoiceMvcInventoryHandlerAndNamedTestWithoutInventingBusinessProof()
    {
        var result = await new SolutionGraphIndexer().AnalyzeSolutionAsync(FixtureSolution, "fixture-sha");
        var signals = result.FeatureSignals.Signals;
        Assert.Equal(4, signals.Count);
        var post = Assert.Single(signals, x =>
            x.Kind == FeatureSignalKind.ControllerAction &&
            x.SuggestedFeatureId == "create-invoice");
        Assert.Equal("POST", post.HttpVerb);
        Assert.Equal("KnownMvcHttpAttributeName;NotRegisteredRoute", post.Detector);
        var get = Assert.Single(signals, x =>
            x.Kind == FeatureSignalKind.ControllerAction && x.HttpVerb == "GET");
        Assert.Equal("{invoiceId}", get.RouteFragment);
        var handler = Assert.Single(signals, x => x.Kind == FeatureSignalKind.NamedHandler);
        Assert.Equal("reserve-inventory", handler.SuggestedFeatureId);
        Assert.Equal("HandlerTypeAndMethodNaming;NotRuntimeRegistration", handler.Detector);
        var test = Assert.Single(signals, x => x.Kind == FeatureSignalKind.TestDeclaration);
        Assert.Contains("CreateInvoice", test.SymbolId, StringComparison.Ordinal);
        Assert.All(signals, x =>
        {
            Assert.Equal(EvidenceKind.Static, x.Source.Kind);
            Assert.Equal(EvidenceKind.Inferred, x.BusinessClaim);
            Assert.Equal("fixture-sha", x.Source.Revision);
            Assert.Contains(result.Graph.Nodes, n => n.SymbolId == x.SymbolId &&
                                                     n.Evidence == x.Source);
        });
        Assert.Equal(FeatureDiscoveryContract.Scope, result.FeatureSignals.DiscoveryScope);
        Assert.Equal("Unknown", result.FeatureSignals.RuntimeStatus);
        Assert.DoesNotContain(signals, x => x.SymbolId.Contains("DeleteInvoices"));
        Assert.DoesNotContain(signals, x => x.SymbolId.Contains("InventoryHelper"));
        Assert.Empty(result.FeatureSignals.Diagnostics);
    }

    [Fact]
    public async Task DuplicateIndexingIsDeterministicAndNeverCreatesBusinessFeatureCatalog()
    {
        var first = await new SolutionGraphIndexer().AnalyzeSolutionAsync(FixtureSolution, "rev-a");
        var second = await new SolutionGraphIndexer().AnalyzeSolutionAsync(FixtureSolution, "rev-a");
        Assert.Equal(first.FeatureSignals.Signals, second.FeatureSignals.Signals);
        using var temp = new DirectoryFixture();
        var graphStore = new GraphSnapshotStore(temp.Path);
        graphStore.Save(first.Graph);
        var signalStore = new FeatureDiscoveryStore(temp.Path);
        Assert.Equal(signalStore.Save(first.Graph, first.FeatureSignals),
            signalStore.Save(first.Graph, first.FeatureSignals));
        var loaded = Assert.IsType<FeatureDiscoveryReport>(signalStore.Load(first.Graph));
        Assert.Equal(4, loaded.Signals.Count);
        Assert.Null(new FeatureCatalogStore(temp.Path).Load(first.Graph));
        Assert.Null(signalStore.Load(first.Graph with { Revision = "rev-other",
            Nodes = first.Graph.Nodes.Select(n => n with
                { Evidence = n.Evidence with { Revision = "rev-other" } }).ToArray(),
            Edges = first.Graph.Edges.Select(e => e with
                { Evidence = e.Evidence with { Revision = "rev-other" } }).ToArray()
        }));
        Assert.Throws<InvalidOperationException>(() => signalStore.Save(first.Graph,
            first.FeatureSignals with { Diagnostics = ["proposed alternative"] }));
    }

    [Fact]
    public async Task RejectsTamperedSourceBusinessEvidenceAndMismatchedRevision()
    {
        var analysis = await new SolutionGraphIndexer().AnalyzeSolutionAsync(FixtureSolution, "rev-1");
        var original = analysis.FeatureSignals;
        var forged = original.Signals.Select((x,i) => i == 0 ? x with
            { BusinessClaim = EvidenceKind.Verified } : x).ToArray();
        Assert.Throws<InvalidDataException>(() =>
            FeatureDiscoveryContract.Validate(analysis.Graph, original with { Signals = forged }));
        Assert.Throws<InvalidDataException>(() =>
            FeatureDiscoveryContract.Validate(analysis.Graph, original with { GraphRevision = "rev-2" }));
        Assert.Throws<InvalidDataException>(() =>
            FeatureDiscoveryContract.Validate(analysis.Graph, original with
                { DiscoveryScope = "AuthenticatedObservedRoutes" }));
        using var temp = new DirectoryFixture();
        var store = new FeatureDiscoveryStore(temp.Path);
        var path = store.Save(analysis.Graph, original);
        File.WriteAllText(path, File.ReadAllText(path).Replace("feature-signal:", "forged-signal:"));
        Assert.Throws<InvalidDataException>(() => store.Load(analysis.Graph));
    }

    [Fact]
    public void NonRegisteredNamingWithoutRecognizedAttributesYieldsNoCandidates()
    {
        var source = "public class Payment { public void Run() {} }";
        var tree = CSharpSyntaxTree.ParseText(source, path: "Payment.cs");
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Sample", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var graph = StaticGraphIndexer.IndexCompilation("Sample", compilation, "v1");
        var tests = MethodTestAssociationIndexer.Project(graph, [("Sample", compilation)]);
        var report = FeatureCandidateIndexer.Discover(graph, tests, [("Sample", compilation)]);
        Assert.Empty(report.Signals);
        Assert.Equal("Unknown", report.RuntimeStatus);
    }

    private sealed class DirectoryFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-t024-tests-" + Guid.NewGuid().ToString("N"));
        public DirectoryFixture() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
