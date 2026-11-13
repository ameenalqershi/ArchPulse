// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class FeatureAtlasTests
{
    private static GraphSnapshot Graph(string revision = "rev-1")
    {
        var ev = new SourceEvidence(revision, "Invoice.cs", 3, 1, EvidenceKind.Static);
        return new GraphSnapshot("Billing", revision,
        [
            new("project:Billing", GraphNodeKind.Project, "Billing", null, ev),
            new("method:Invoice.Create", GraphNodeKind.Method, "Create()", "project:Billing", ev),
            new("method:Invoice.Save", GraphNodeKind.Method, "Save()", "project:Billing", ev),
            new("method:Invoice.Delete", GraphNodeKind.Method, "Delete()", "project:Billing", ev)
        ],
        [
            new("call-create-save", "method:Invoice.Create", "method:Invoice.Save", "Calls", ev),
            new("call-delete-save", "method:Invoice.Delete", "method:Invoice.Save", "Calls", ev),
            new("guessed", "method:Invoice.Create", "method:Invoice.Delete", "Calls",
                ev with { Kind = EvidenceKind.Inferred })
        ], []);
    }

    private static BusinessFeature Feature(string id = "create-invoice") =>
        new(id, "Create Invoice", "Explicit operator-curated feature scope.", "Accounts team",
            FeatureCriticality.P0, ["Invoice exists"],
            ["Review state and error response"],
            [
                new("method:Invoice.Create", "Candidate entry point", EvidenceKind.Designed),
                new("method:Invoice.Save", "Candidate persistence call", EvidenceKind.Inferred)
            ]);

    private static FeatureCatalog Catalog(string revision = "rev-1", params BusinessFeature[] features) =>
        new(FeatureAtlas.Schema, "Billing", revision,
            features.Length == 0 ? [Feature()] : features);

    [Fact]
    public void ExactManualFeatureOnlyProjectsSourceBackedInternalEdges()
    {
        var result = FeatureAtlas.Project(Graph(), Catalog(), "create-invoice");
        Assert.Equal(FeatureAtlas.ScopeSchema, result.Schema);
        Assert.Equal("rev-1", result.Revision);
        Assert.Equal(2, result.Nodes.Count);
        var edge = Assert.Single(result.StaticEdges);
        Assert.Equal("call-create-save", edge.EdgeId);
        Assert.Equal(EvidenceKind.Static, edge.Evidence.Kind);
        Assert.Equal("Unknown", result.ExecutionStatus);
        Assert.False(result.ObservedCoverage);
        Assert.All(result.Feature.Members, m => Assert.True(
            m.Claim is EvidenceKind.Designed or EvidenceKind.Inferred));
    }

    [Fact]
    public void OverlappingFeaturesDoNotReparentOrDuplicateCanonicalNodes()
    {
        var other = Feature("refund-invoice") with
        {
            Members = [new FeatureMembership("method:Invoice.Save", "Shared service", EvidenceKind.Designed)]
        };
        var graph = Graph();
        var catalog = Catalog(features: [Feature(), other]);
        FeatureAtlas.Validate(graph, catalog);
        var source = Assert.Single(graph.Nodes, n => n.SymbolId == "method:Invoice.Save");
        Assert.Equal("project:Billing", source.ParentSymbolId);
        Assert.Single(FeatureAtlas.Project(graph, catalog, other.Id).Nodes);
    }

    [Fact]
    public void MissingCatalogIsNotInventedAndStoredCatalogIsImmutable()
    {
        using var temp = new TempDir();
        var store = new FeatureCatalogStore(temp.Directory);
        var graph = Graph();
        Assert.Null(store.Load(graph));
        var catalog = Catalog();
        Assert.Equal(store.Save(graph, catalog), store.Save(graph, catalog));
        var loaded = Assert.IsType<FeatureCatalog>(store.Load(graph));
        Assert.Single(loaded.Features);
        Assert.Throws<InvalidOperationException>(() => store.Save(graph, Catalog(features:
            [Feature() with { Description = "Changed business meaning" }])));
        Assert.Null(store.Load(Graph("rev-2")));
    }

    [Fact]
    public void ExactRevisionMismatchIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => FeatureAtlas.Validate(Graph(), Catalog("rev-2")));
    }

    [Fact]
    public void FabricatedOrDuplicateMembersAreRejected()
    {
        var fake = Feature() with
        {
            Members = [new FeatureMembership("method:Unknown", "Not indexed", EvidenceKind.Designed)]
        };
        Assert.Throws<InvalidDataException>(() => FeatureAtlas.Validate(Graph(), Catalog(features: [fake])));
        var duplicate = Feature() with { Members = [Feature().Members[0], Feature().Members[0]] };
        Assert.Throws<InvalidDataException>(() =>
            FeatureAtlas.Validate(Graph(), Catalog(features: [duplicate])));
    }

    [Theory]
    [InlineData(EvidenceKind.Static)]
    [InlineData(EvidenceKind.Verified)]
    [InlineData(EvidenceKind.Observed)]
    [InlineData(EvidenceKind.Unknown)]
    public void HumanBusinessMembershipCannotClaimEvidenceNotEstablished(EvidenceKind kind)
    {
        var bad = Feature() with
        {
            Members = [new FeatureMembership("method:Invoice.Create", "Unsupported", kind)]
        };
        Assert.Throws<InvalidDataException>(() => FeatureAtlas.Validate(Graph(), Catalog(features: [bad])));
    }

    [Fact]
    public void DuplicateFeatureIdsInvalidSlugsAndNullCollectionsAreRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            FeatureAtlas.Validate(Graph(), Catalog(features: [Feature(), Feature()])));
        Assert.Throws<InvalidDataException>(() =>
            FeatureAtlas.Validate(Graph(), Catalog(features: [Feature("../traversal")])));
        Assert.Throws<InvalidDataException>(() =>
            FeatureAtlas.Validate(Graph(), Catalog(features: [Feature() with { Scenarios = null! }])));
        Assert.Throws<InvalidDataException>(() =>
            FeatureAtlas.Validate(Graph(), Catalog(features: [Feature() with { Members = [] }])));
    }

    [Fact]
    public void UnknownFeatureIsNotInvented()
    {
        Assert.Throws<KeyNotFoundException>(() => FeatureAtlas.Project(Graph(), Catalog(), "refund"));
    }

    [Fact]
    public void MalformedAndOversizeManifestsRejectBeforePersistence()
    {
        Assert.Throws<JsonException>(() => FeatureCatalogStore.Parse("{invalid"));
        Assert.Throws<InvalidDataException>(() => FeatureCatalogStore.Parse(new string('a', 2_000_001)));
        var invalidEnum = JsonSerializer.Serialize(Catalog()).Replace("\"P0\"", "99", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => FeatureCatalogStore.Parse(invalidEnum));
    }

    [Fact]
    public void TamperedSidecarIsRejectedAtReadTime()
    {
        using var temp = new TempDir();
        var graph = Graph();
        var store = new FeatureCatalogStore(temp.Directory);
        var saved = store.Save(graph, Catalog());
        File.WriteAllText(saved, File.ReadAllText(saved).Replace("method:Invoice.Create", "method:Missing"));
        Assert.Throws<InvalidDataException>(() => store.Load(graph));
    }

    private sealed class TempDir : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-feature-catalog-" + Guid.NewGuid().ToString("N"));
        public TempDir() => System.IO.Directory.CreateDirectory(Directory);
        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
