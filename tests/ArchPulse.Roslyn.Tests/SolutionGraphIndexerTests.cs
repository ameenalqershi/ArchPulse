// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class SolutionGraphIndexerTests
{
    private static string Root
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ArchPulse.sln")))
                    return directory.FullName;
            }
            throw new DirectoryNotFoundException("Cannot find repository root.");
        }
    }

    [Fact]
    public async Task IndexesRealRestoredSolutionAndCrossProjectCalls()
    {
        string path = Path.Combine(Root, "examples", "billing-multi-project", "Billing.sln");
        var graph = await new SolutionGraphIndexer().IndexSolutionAsync(path, "test-revision");
        GraphSnapshotStore.Validate(graph);
        Assert.Equal(11, graph.Nodes.Count);
        Assert.Equal(3, graph.Edges.Count);
        Assert.Contains(graph.Nodes, n => n.Kind == GraphNodeKind.Solution);
        Assert.Contains(graph.Edges, e =>
            e.Relation == "DependsOn"
            && e.FromSymbolId == "project:Billing.Service"
            && e.ToSymbolId == "project:Billing.Contracts");
        var call = Assert.Single(graph.Edges, e =>
            e.Relation == "Calls" && e.ToSymbolId.StartsWith("Billing.Contracts::", StringComparison.Ordinal));
        Assert.StartsWith("Billing.Service::", call.FromSymbolId, StringComparison.Ordinal);
        Assert.Equal(EvidenceKind.Static, call.Evidence.Kind);
        Assert.Contains("/Billing.Service/InvoiceService.cs", "/" + call.Evidence.RelativeFile,
            StringComparison.Ordinal);
        Assert.All(graph.Nodes, n => Assert.Equal("test-revision", n.Evidence.Revision));
        Assert.DoesNotContain(graph.Diagnostics, d => d.StartsWith("MSBuild workspace Failure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingSolutionMustFailInsteadOfReturningEmptyGraph()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new SolutionGraphIndexer().IndexSolutionAsync("/definitely-missing/Billing.sln", "rev"));
    }

    [Fact]
    public async Task RevisionIsExplicitAndStable()
    {
        string path = Path.Combine(Root, "examples", "billing-multi-project", "Billing.sln");
        var indexer = new SolutionGraphIndexer();
        var first = await indexer.IndexSolutionAsync(path, "rev1");
        var second = await indexer.IndexSolutionAsync(path, "rev2");
        Assert.Equal(first.Nodes.Select(n => n.SymbolId), second.Nodes.Select(n => n.SymbolId));
        Assert.All(second.Edges, e => Assert.Equal("rev2", e.Evidence.Revision));
        Assert.NotEqual(first.Revision, second.Revision);
    }
}
