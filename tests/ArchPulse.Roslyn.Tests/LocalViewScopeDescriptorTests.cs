// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class LocalViewScopeDescriptorTests
{
    private static GraphSnapshot Snapshot(string revision = "revision-one")
    {
        var source = new SourceEvidence(revision, "Local.cs", 1, 1, EvidenceKind.Static);
        return new GraphSnapshot("Project", revision,
        [
            new("root", GraphNodeKind.Solution, "Root", null, source),
            new("child", GraphNodeKind.Method, "Method", "root", source)
        ], [], []);
    }

    [Fact]
    public void BindsExactScopeLensAndRevisionWithoutImplyingPermission()
    {
        var result = LocalViewScopeDescriptor.Create(Snapshot(), "child", "dependency",
            maxNodes: 20, maxEdges: 10, depth: 0);
        Assert.Equal(LocalViewScopeDescriptor.ContractSchema, result.Schema);
        Assert.Equal("Project", result.Project);
        Assert.Equal("revision-one", result.GraphRevision);
        Assert.Equal("child", result.RootSymbolId);
        Assert.Equal("dependency", result.Lens);
        Assert.Equal(20, result.MaxNodes);
        Assert.Equal(10, result.MaxEdges);
        Assert.Equal(0, result.Depth);
        Assert.Equal("AuthorizationNotEvaluated", result.AccessStatus);
        Assert.Equal("revision-two",
            LocalViewScopeDescriptor.Create(Snapshot("revision-two"), "child").GraphRevision);
    }

    [Theory]
    [InlineData("", "architecture", 100, 200, 1)]
    [InlineData(" ", "architecture", 100, 200, 1)]
    [InlineData("root", "runtime", 100, 200, 1)]
    [InlineData("root", "Architecture", 100, 200, 1)]
    [InlineData("root", "architecture", 0, 200, 1)]
    [InlineData("root", "architecture", 1001, 200, 1)]
    [InlineData("root", "architecture", 100, 0, 1)]
    [InlineData("root", "architecture", 100, 2001, 1)]
    [InlineData("root", "architecture", 100, 200, -1)]
    [InlineData("root", "architecture", 100, 200, 9)]
    public void RefusesInvalidOrUnsupportedDescriptors(
        string id, string lens, int maxNodes, int maxEdges, int depth)
    {
        Assert.ThrowsAny<ArgumentException>(() => LocalViewScopeDescriptor.Create(
            Snapshot(), id, lens, maxNodes, maxEdges, depth));
    }

    [Fact]
    public void CannotUseNonexistentOrOversizedExactSymbols()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            LocalViewScopeDescriptor.Create(Snapshot(), "unknown"));
        Assert.Throws<ArgumentException>(() =>
            LocalViewScopeDescriptor.Create(Snapshot(), new string('a', 2049)));
    }
}
