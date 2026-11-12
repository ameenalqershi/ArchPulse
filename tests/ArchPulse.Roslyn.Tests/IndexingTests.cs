// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public class IndexingTests
{
    private const string FixtureRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task RoslynIndexesMethodsOverloadsAndObservedSourceCalls()
    {
        using var fixture = new ProjectFixture();
        var snapshot = await new RoslynRepositoryIndexer().IndexAsync(fixture.Project, FixtureRevision);
        Assert.Empty(snapshot.VerifyIntegrity());
        Assert.Contains(snapshot.Nodes, n => n.Kind == GraphNodeKind.Class && n.Name.Contains("InvoiceService"));
        var methods = snapshot.Nodes.Where(n =>
            n.Kind == GraphNodeKind.Method && n.Name.Contains("InvoiceService.")).ToArray();
        Assert.True(methods.Length >= 3, $"Expected methods but found: {string.Join(", ", methods.Select(m => m.Name))}");
        Assert.Equal(methods.Length, methods.Select(m => m.Id).Distinct().Count());
        var createMethod = Assert.Single(methods, n => n.Name.Contains("Create(int)"));
        Assert.NotNull(createMethod.Evidence.Source);
        Assert.Equal(EvidenceKind.Static, createMethod.Evidence.Kind);
        Assert.Equal(FixtureRevision, createMethod.Evidence.Revision);
        var outgoing = snapshot.Edges.Where(e => e.From == createMethod.Id &&
            e.Kind == GraphEdgeKind.Calls).ToArray();
        Assert.NotEmpty(outgoing);
        Assert.Contains(outgoing, e => snapshot.Nodes.Single(n => n.Id == e.To).Name.Contains("Save("));
        Assert.All(outgoing, edge => Assert.True(edge.Evidence.Source!.StartLine > 0));
    }

    [Fact]
    public async Task ReindexSameSourcesProducesSameIdentities()
    {
        using var fixture = new ProjectFixture();
        var analyzer = new RoslynRepositoryIndexer();
        var a = await analyzer.IndexAsync(fixture.Project, FixtureRevision);
        var b = await analyzer.IndexAsync(fixture.Project, FixtureRevision);
        Assert.Equal(a.Nodes.Select(n => n.Id), b.Nodes.Select(n => n.Id));
        Assert.Equal(a.Edges.Select(n => n.Id), b.Edges.Select(n => n.Id));
        Assert.Equal(a.ToMermaid(), b.ToMermaid());
    }

    [Fact]
    public async Task RejectsMissingRevisionInsteadOfInventingEvidence()
    {
        using var fixture = new ProjectFixture();
        await Assert.ThrowsAsync<ArgumentException>(
            () => new RoslynRepositoryIndexer().IndexAsync(fixture.Project, "fake"));
    }

    private sealed class ProjectFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "archpulse-roslyn-" + Guid.NewGuid().ToString("N"));
        public string Project => Path.Combine(_directory, "Example.csproj");

        public ProjectFixture()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Project, "<Project Sdk=\"Microsoft.NET.Sdk\">" +
                "<PropertyGroup><TargetFramework>net10.0</TargetFramework>" +
                "<Nullable>enable</Nullable></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(_directory, "InvoiceService.cs"),
                """
                // SPDX-License-Identifier: Apache-2.0
                namespace Acme.Invoice;
                public interface IInvoiceStore { void Save(int id); }
                public sealed class InvoiceService(IInvoiceStore store)
                {
                    public void Create(int id) { store.Save(id); }
                    public void Create(string invoiceNo) { _ = invoiceNo.Length; }
                    public int Count() => 42;
                }
                """);
            var process = Process.Start(new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "restore", Project, "--ignore-failed-sources" },
                WorkingDirectory = _directory, RedirectStandardError = true,
                RedirectStandardOutput = true, UseShellExecute = false
            }) ?? throw new InvalidOperationException("Could not restore test fixture");
            using (process)
            {
                process.WaitForExit(60000);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Fixture restore failed: " + process.StandardError.ReadToEnd());
            }
        }
        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
