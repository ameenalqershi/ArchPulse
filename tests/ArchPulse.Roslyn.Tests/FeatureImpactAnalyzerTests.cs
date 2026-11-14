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
    public void RetainedNodeIdentityWithChangedLabelAndLocationIsAnUnverifiedCandidateImpact()
    {
        var before = Graph("before");
        var after = Graph("after");
        after = after with
        {
            Nodes = after.Nodes.Select(n => n.SymbolId == "method:Save"
                ? n with
                {
                    Label = "SaveAsync",
                    Evidence = n.Evidence with { Line = 24 }
                } : n).ToArray()
        };

        var diff = GraphSnapshotStore.Compare(before, after);
        Assert.Equal(["method:Save"], diff.ChangedNodes);
        var result = FeatureImpactAnalyzer.Assess(before, after, Catalog(before));
        Assert.Equal(2, result.StructuralChangesExamined);
        var beforeImpact = Assert.Single(result.Impacts,
            x => x.ChangeKind == StructuralChangeKind.NodeModifiedBefore);
        var afterImpact = Assert.Single(result.Impacts,
            x => x.ChangeKind == StructuralChangeKind.NodeModifiedAfter);
        Assert.Equal("before", beforeImpact.ChangeEvidence.Revision);
        Assert.Equal("after", afterImpact.ChangeEvidence.Revision);
        Assert.Equal(24, afterImpact.ChangeEvidence.Line);
        Assert.All(result.Impacts, impact =>
        {
            Assert.Equal("method:Save", impact.ChangedSymbolId);
            Assert.Equal("create-invoice", impact.FeatureId);
            Assert.Equal(FeatureImpactBasis.StaticCallerPath, impact.Basis);
            Assert.Single(impact.StaticCallWitness);
        });
        Assert.Equal("Unknown", result.RuntimeStatus);
    }

    [Fact]
    public void RetainedCallIdentityChangedDestinationPreservesBeforeAndAfterEvidence()
    {
        var before = Graph("before");
        var after = Graph("after") with
        {
            Edges =
            [
                new GraphEdge("call:create-save", "method:Create", "method:Refund",
                    "Calls", new SourceEvidence("after", "Billing.cs", 30, 1, EvidenceKind.Static))
            ]
        };
        Assert.Equal(["call:create-save"], GraphSnapshotStore.Compare(before, after).ChangedEdges);

        var result = FeatureImpactAnalyzer.Assess(before, after, Catalog(before));
        Assert.Equal(2, result.StructuralChangesExamined);
        var oldSide = Assert.Single(result.Impacts,
            x => x.ChangeKind == StructuralChangeKind.CallModifiedBefore);
        var newSide = Assert.Single(result.Impacts,
            x => x.ChangeKind == StructuralChangeKind.CallModifiedAfter);
        Assert.Equal("before", oldSide.ChangeEvidence.Revision);
        Assert.Equal(10, oldSide.ChangeEvidence.Line);
        Assert.Equal("after", newSide.ChangeEvidence.Revision);
        Assert.Equal(30, newSide.ChangeEvidence.Line);
        Assert.Equal("method:Create", oldSide.ChangedSymbolId);
        Assert.Equal("method:Create", newSide.ChangedSymbolId);
        Assert.Empty(oldSide.StaticCallWitness);
        Assert.Empty(newSide.StaticCallWitness);

        // An inferred new-side relation cannot be promoted into Static evidence.
        var inferred = after with
        {
            Edges = after.Edges.Select(e => e with
            { Evidence = e.Evidence with { Kind = EvidenceKind.Inferred } }).ToArray()
        };
        var limited = FeatureImpactAnalyzer.Assess(before, inferred, Catalog(before));
        Assert.Equal(1, limited.StructuralChangesExamined);
        Assert.Single(limited.Impacts,
            x => x.ChangeKind == StructuralChangeKind.CallModifiedBefore);
        Assert.DoesNotContain(limited.Impacts,
            x => x.ChangeKind == StructuralChangeKind.CallModifiedAfter);
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
    public void RemovedAndAddedCallsKeepTheirSeparateRevisionWitnesses()
    {
        var beforeEvidence = new SourceEvidence("before", "Before.cs", 2, 1, EvidenceKind.Static);
        var afterEvidence = new SourceEvidence("after", "After.cs", 3, 1, EvidenceKind.Static);
        var before = new GraphSnapshot("Mixed", "before",
        [
            new("entry", GraphNodeKind.Method, "Entry", null, beforeEvidence),
            new("removed", GraphNodeKind.Method, "Removed", null, beforeEvidence)
        ],
        [
            new("old-call", "entry", "removed", "Calls", beforeEvidence)
        ], []);
        var after = new GraphSnapshot("Mixed", "after",
        [
            new("entry", GraphNodeKind.Method, "Entry", null, afterEvidence),
            new("added", GraphNodeKind.Method, "Added", null, afterEvidence)
        ],
        [
            new("new-call", "entry", "added", "Calls", afterEvidence)
        ], []);
        var catalog = new FeatureCatalog(FeatureAtlas.Schema, "Mixed", "before",
        [
            new BusinessFeature("mixed", "Mixed", "Candidate", "Owner",
                FeatureCriticality.P1, ["Entry"], ["Review"],
                [new FeatureMembership("entry", "Proposed", EvidenceKind.Designed)])
        ]);
        var report = FeatureImpactAnalyzer.Assess(before, after, catalog);
        // The retained 'entry' symbol also changed its evidence location.
        Assert.Equal(6, report.StructuralChangesExamined);
        Assert.Equal(6, report.Impacts.Count);
        Assert.Single(report.Impacts, x => x.ChangeKind == StructuralChangeKind.NodeModifiedBefore);
        Assert.Single(report.Impacts, x => x.ChangeKind == StructuralChangeKind.NodeModifiedAfter);
        var removed = Assert.Single(report.Impacts, x =>
            x.ChangeKind == StructuralChangeKind.NodeRemoved);
        Assert.Equal("removed", removed.ChangedSymbolId);
        var oldCall = Assert.Single(removed.StaticCallWitness);
        Assert.Equal("old-call", oldCall.EdgeId);
        Assert.Equal("before", oldCall.Evidence.Revision);
        var added = Assert.Single(report.Impacts, x =>
            x.ChangeKind == StructuralChangeKind.NodeAdded);
        Assert.Equal("added", added.ChangedSymbolId);
        var newCall = Assert.Single(added.StaticCallWitness);
        Assert.Equal("new-call", newCall.EdgeId);
        Assert.Equal("after", newCall.Evidence.Revision);
        Assert.All(report.Impacts, x => Assert.Equal("Unknown", report.RuntimeStatus));
    }

    [Fact]
    public void ManyIndependentChangesRemainCompleteAndDeterministic()
    {
        const int changed = 120;
        // Keep the retained entry's source location identical across revisions,
        // so the stress fixture isolates exactly 120 additions and 120 calls.
        var beforeEvidence = new SourceEvidence("v1", "New.cs", 1, 1, EvidenceKind.Static);
        var afterEvidence = new SourceEvidence("v2", "New.cs", 1, 1, EvidenceKind.Static);
        var before = new GraphSnapshot("Wide", "v1",
            [new("entry", GraphNodeKind.Method, "Entry", null, beforeEvidence)], [], []);
        var addedNodes = Enumerable.Range(0, changed)
            .Select(index => new GraphNode($"method:{index:000}", GraphNodeKind.Method,
                $"Method {index}", null, afterEvidence)).ToArray();
        var addedEdges = Enumerable.Range(0, changed)
            .Select(index => new GraphEdge($"call:{index:000}", "entry",
                $"method:{index:000}", "Calls", afterEvidence)).ToArray();
        var after = new GraphSnapshot("Wide", "v2",
            addedNodes.Reverse().Append(new GraphNode("entry", GraphNodeKind.Method,
                "Entry", null, afterEvidence)).ToArray(),
            addedEdges.Reverse().ToArray(), []);
        var catalog = new FeatureCatalog(FeatureAtlas.Schema, "Wide", "v1",
        [
            new BusinessFeature("entry-feature", "Entry Feature", "Candidate", "Owner",
                FeatureCriticality.P1, ["Entry"], ["Review"],
                [new FeatureMembership("entry", "Proposed", EvidenceKind.Inferred)])
        ]);

        var one = FeatureImpactAnalyzer.Assess(before, after, catalog);
        var two = FeatureImpactAnalyzer.Assess(before, after, catalog);
        Assert.Equal(changed * 2, one.StructuralChangesExamined);
        // The existing impact contract intentionally deduplicates change events
        // with the same feature/member/changed-symbol/kind/source span. Here all
        // CallAdded edges share the same caller and span, so they collapse to one
        // candidate signal (while all 240 events are still examined).
        Assert.Equal(changed + 1, one.Impacts.Count);
        Assert.Equal(changed, one.Impacts.Count(x => x.ChangeKind == StructuralChangeKind.NodeAdded));
        Assert.Single(one.Impacts, x => x.ChangeKind == StructuralChangeKind.CallAdded);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(one),
            System.Text.Json.JsonSerializer.Serialize(two));
        Assert.All(one.Impacts, x =>
        {
            Assert.Equal("entry-feature", x.FeatureId);
            Assert.All(x.StaticCallWitness, edge => Assert.Equal(EvidenceKind.Static, edge.Evidence.Kind));
        });
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
