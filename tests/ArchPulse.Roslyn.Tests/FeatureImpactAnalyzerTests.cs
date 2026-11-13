// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class FeatureImpactAnalyzerTests
{
    private static GraphSnapshot Graph(string revision, bool changed = false,
        bool inferred = false, bool removedSave = false)
    {
        var ev = new SourceEvidence(revision, "Billing.cs", 10, 1, EvidenceKind.Static);
        var nodes = new List<GraphNode>
        {
            new("project:Billing", GraphNodeKind.Project, "Billing", null, ev),
            new("method:Create", GraphNodeKind.Method, "Create", "project:Billing", ev),
            new("method:Refund", GraphNodeKind.Method, "Refund", "project:Billing", ev)
        };
        if (!removedSave)
            nodes.Add(new("method:Save", GraphNodeKind.Method, "Save", "project:Billing", ev));
        if (changed)
            nodes.Add(new("method:Reserve", GraphNodeKind.Method, "Reserve", "project:Billing", ev));
        var edges = new List<GraphEdge>();
        if (!removedSave)
            edges.Add(new("call:create-save", "method:Create", "method:Save", "Calls", ev));
        if (changed)
            edges.Add(new("call:save-reserve", "method:Save", "method:Reserve",
                "Calls", ev with { Kind = inferred ? EvidenceKind.Inferred : EvidenceKind.Static }));
        return new GraphSnapshot("Billing", revision, nodes, edges, []);
    }

    private static FeatureCatalog Catalog(GraphSnapshot old) =>
        new(FeatureAtlas.Schema, old.Project, old.Revision,
        [
            new BusinessFeature("create-invoice", "Create Invoice", "Candidate flow",
                "Billing team", FeatureCriticality.P0, ["Submit"], ["Review"],
                [new FeatureMembership("method:Create", "Proposed entry", EvidenceKind.Inferred)]),
            new BusinessFeature("refund-invoice", "Refund Invoice", "Candidate refund",
                "Billing team", FeatureCriticality.P1, ["Refund"], ["Review"],
                [new FeatureMembership("method:Refund", "Separate flow", EvidenceKind.Designed)])
        ]);

    [Fact]
    public void StructuralChangeHasDirectAndStaticPathHintsButNoVerifiedBusinessImpact()
    {
        var before = Graph("sha-before");
        var after = Graph("sha-after", changed: true);
        var report = FeatureImpactAnalyzer.Assess(before, after, Catalog(before));
        Assert.Equal(FeatureImpactAnalyzer.Schema, report.Schema);
        Assert.Equal("sha-before", report.CatalogRevision);
        Assert.Equal("Unknown", report.RuntimeStatus);
        Assert.Equal(FeatureImpactAnalyzer.Scope, report.EvidenceScope);
        Assert.Equal(2, report.StructuralChangesExamined);
        Assert.Equal(2, report.Impacts.Count);
        Assert.All(report.Impacts, item =>
        {
            Assert.Equal("create-invoice", item.FeatureId);
            Assert.Equal("method:Create", item.MemberSymbolId);
            Assert.Equal(FeatureImpactBasis.StaticCallerPath, item.Basis);
            Assert.NotEmpty(item.StaticCallWitness);
            Assert.All(item.StaticCallWitness, edge =>
                Assert.Equal(EvidenceKind.Static, edge.Evidence.Kind));
        });
        var added = Assert.Single(report.Impacts, x => x.ChangeKind == StructuralChangeKind.NodeAdded);
        Assert.Equal("method:Reserve", added.ChangedSymbolId);
        Assert.Equal(2, added.StaticCallWitness.Count);
        var call = Assert.Single(report.Impacts, x => x.ChangeKind == StructuralChangeKind.CallAdded);
        Assert.Equal("method:Save", call.ChangedSymbolId);
        Assert.Single(call.StaticCallWitness);
        Assert.DoesNotContain(report.Impacts, x => x.FeatureId == "refund-invoice");
    }

    [Fact]
    public void RemovedMemberIsDirectCandidateAndRemovedStaticCallMayAffectIt()
    {
        var before = Graph("old");
        var after = Graph("new", removedSave: true);
        var catalog = Catalog(before) with
        {
            Features = [Catalog(before).Features[0] with
                { Members = [new FeatureMembership("method:Save", "Shared sink", EvidenceKind.Designed)] }]
        };
        var report = FeatureImpactAnalyzer.Assess(before, after, catalog);
        Assert.Contains(report.Impacts, x => x.ChangeKind == StructuralChangeKind.NodeRemoved
            && x.ChangedSymbolId == "method:Save"
            && x.Basis == FeatureImpactBasis.DirectCandidateMember
            && x.StaticCallWitness.Count == 0);
        Assert.DoesNotContain(report.Impacts, x => x.ChangeKind == StructuralChangeKind.NodeAdded);
    }

    [Fact]
    public void InferredCallCannotProduceAStaticWitnessAndLimitedHopsAreHonored()
    {
        var before = Graph("old");
        var after = Graph("new", changed: true, inferred: true);
        var report = FeatureImpactAnalyzer.Assess(before, after, Catalog(before));
        Assert.DoesNotContain(report.Impacts, x => x.ChangeKind == StructuralChangeKind.NodeAdded);
        Assert.DoesNotContain(report.Impacts, x => x.ChangeKind == StructuralChangeKind.CallAdded);
        Assert.Empty(FeatureImpactAnalyzer.Assess(before,
            Graph("new", changed: true), Catalog(before), maxCallHops: 0).Impacts);
    }

    [Fact]
    public void InferredNodesCannotBecomeSourceBackedStructuralImpact()
    {
        var before = Graph("before");
        var syntactic = Graph("after", changed: true, inferred: true);
        var after = syntactic with
        {
            Nodes = syntactic.Nodes.Select(n => n.SymbolId == "method:Reserve"
                ? n with { Evidence = n.Evidence with { Kind = EvidenceKind.Inferred } }
                : n).ToArray()
        };
        var report = FeatureImpactAnalyzer.Assess(before, after, Catalog(before));
        Assert.Empty(report.Impacts);
        Assert.Equal(0, report.StructuralChangesExamined);
    }

    [Fact]
    public void IdenticalTopologyWithChangedSourceLocationsIsNotAClaimOfChangedBehavior()
    {
        var before = Graph("old");
        var after = Graph("new");
        var report = FeatureImpactAnalyzer.Assess(before, after, Catalog(before));
        Assert.Equal(0, report.StructuralChangesExamined);
        Assert.Empty(report.Impacts);
    }

    [Fact]
    public void RejectsWrongCatalogIdentityProjectRevisionAndInvalidLimits()
    {
        var before = Graph("old");
        var after = Graph("new", changed: true);
        var catalog = Catalog(before);
        Assert.Throws<InvalidDataException>(() =>
            FeatureImpactAnalyzer.Assess(before, after, catalog with { Revision = "wrong" }));
        Assert.Throws<InvalidDataException>(() =>
            FeatureImpactAnalyzer.Assess(before, before, catalog));
        Assert.Throws<InvalidDataException>(() =>
            FeatureImpactAnalyzer.Assess(before, after with { Project = "Other" }, catalog));
        Assert.Throws<InvalidDataException>(() =>
            FeatureImpactAnalyzer.Assess(before, after, catalog, maxCallHops: 9));
    }

    [Fact]
    public void ReportIsDeterministicAndSharedMembersCanYieldSeparateFeatureCandidates()
    {
        var before = Graph("old");
        var after = Graph("new", changed: true);
        var first = Catalog(before);
        var second = first.Features[1] with
        {
            Members = [new FeatureMembership("method:Create", "Shared method", EvidenceKind.Designed)]
        };
        var shared = first with { Features = [first.Features[0], second] };
        var one = FeatureImpactAnalyzer.Assess(before, after, shared);
        var two = FeatureImpactAnalyzer.Assess(before, after, shared);
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(one),
            System.Text.Json.JsonSerializer.Serialize(two));
        Assert.Equal(2, one.Impacts.Select(i => i.FeatureId).Distinct().Count());
        Assert.Equal(4, one.Impacts.Count);
    }
}
