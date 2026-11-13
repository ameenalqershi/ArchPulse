// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

/// <summary>
/// Structural change signals only. A graph difference is not a Git patch or
/// proof that a method's body/behavior changed. Membership remains an operator
/// proposal; a static witness path is not a runtime invocation or business test.
/// </summary>
public enum FeatureImpactBasis { DirectCandidateMember, StaticCallerPath }
public enum StructuralChangeKind { NodeAdded, NodeRemoved, CallAdded, CallRemoved }

public sealed record FeatureImpactSignal(
    string FeatureId, string MemberSymbolId, string ChangedSymbolId,
    StructuralChangeKind ChangeKind, FeatureImpactBasis Basis,
    SourceEvidence ChangeEvidence, IReadOnlyList<GraphEdge> StaticCallWitness,
    string Meaning);

public sealed record FeatureImpactReport(
    string Schema, string Project, string FromRevision, string ToRevision,
    string CatalogRevision, string EvidenceScope, string RuntimeStatus,
    int StructuralChangesExamined, IReadOnlyList<FeatureImpactSignal> Impacts);

public static class FeatureImpactAnalyzer
{
    public const string Schema = "archpulse.feature-impact/v1";
    public const string Scope =
        "CandidateImpact;StructureOnly;NotGitPRDiff;NotObservedRuntimeOrApproval";
    private const int MaxEvents = 500;
    private const int MaxGraphNodes = 40_000;
    private const int MaxGraphEdges = 100_000;
    private const int MaxImpacts = 10_000;

    /// <summary>
    /// Both snapshots must be for the same logical project. The human catalog
    /// belongs to the FROM revision; absent membership is not auto-inferred.
    /// Call paths follow only source-supported directed Calls relations.
    /// </summary>
    public static FeatureImpactReport Assess(
        GraphSnapshot before, GraphSnapshot after, FeatureCatalog beforeCatalog,
        int maxCallHops = 5)
    {
        GraphSnapshotStore.Validate(before);
        GraphSnapshotStore.Validate(after);
        FeatureAtlas.Validate(before, beforeCatalog);
        if (!string.Equals(before.Project, after.Project, StringComparison.Ordinal) ||
            string.Equals(before.Revision, after.Revision, StringComparison.Ordinal))
            throw new InvalidDataException("Feature impact requires two different revisions of the same project.");
        if (maxCallHops is < 0 or > 8 ||
            before.Nodes.Count > MaxGraphNodes || after.Nodes.Count > MaxGraphNodes ||
            before.Edges.Count > MaxGraphEdges || after.Edges.Count > MaxGraphEdges)
            throw new InvalidDataException("Feature-impact graph or hop bounds exceeded.");

        var diff = GraphSnapshotStore.Compare(before, after);
        var beforeNodes = before.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        var afterNodes = after.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        var beforeEdges = before.Edges.ToDictionary(e => e.EdgeId, StringComparer.Ordinal);
        var afterEdges = after.Edges.ToDictionary(e => e.EdgeId, StringComparer.Ordinal);
        var events = new List<ChangeEvent>();
        events.AddRange(diff.AddedNodes
            .Where(x => afterNodes.TryGetValue(x, out var node) &&
                node.Evidence.Kind is EvidenceKind.Static or EvidenceKind.Verified)
            .Select(x => new ChangeEvent(x, StructuralChangeKind.NodeAdded,
                afterNodes[x].Evidence, after)));
        events.AddRange(diff.RemovedNodes
            .Where(x => beforeNodes.TryGetValue(x, out var node) &&
                node.Evidence.Kind is EvidenceKind.Static or EvidenceKind.Verified)
            .Select(x => new ChangeEvent(x, StructuralChangeKind.NodeRemoved,
                beforeNodes[x].Evidence, before)));
        events.AddRange(diff.AddedEdges.Select(x => afterEdges[x])
            .Where(e => e.Relation == "Calls" &&
                e.Evidence.Kind is EvidenceKind.Static or EvidenceKind.Verified)
            .Select(e => new ChangeEvent(e.FromSymbolId, StructuralChangeKind.CallAdded,
                e.Evidence, after)));
        events.AddRange(diff.RemovedEdges.Select(x => beforeEdges[x])
            .Where(e => e.Relation == "Calls" &&
                e.Evidence.Kind is EvidenceKind.Static or EvidenceKind.Verified)
            .Select(e => new ChangeEvent(e.FromSymbolId, StructuralChangeKind.CallRemoved,
                e.Evidence, before)));
        if (events.Count > MaxEvents)
            throw new InvalidDataException("Too many structural deltas: narrow the revision interval.");

        var impacts = new List<FeatureImpactSignal>();
        var unique = new HashSet<(string Feature, string Member, string Changed,
            StructuralChangeKind Kind, string File, int Line, int Column)>();
        foreach (var entry in events.OrderBy(x => x.Kind).ThenBy(x => x.Changed, StringComparer.Ordinal)
                     .ThenBy(x => x.Evidence.RelativeFile, StringComparer.Ordinal)
                     .ThenBy(x => x.Evidence.Line).ThenBy(x => x.Evidence.Column))
        {
            // Each revision's call edges are indexed once per event. This avoids
            // repeated quadratic path-finding over large shared feature catalogs.
            var reverse = BuildReverseCallGraph(entry.Graph);
            var ancestors = ReversePaths(reverse, entry.Changed, maxCallHops);
            foreach (var feature in beforeCatalog.Features.OrderBy(f => f.Id, StringComparer.Ordinal))
            foreach (var member in feature.Members.OrderBy(m => m.SymbolId, StringComparer.Ordinal))
            {
                if (!ancestors.TryGetValue(member.SymbolId, out var path)) continue;
                var basis = member.SymbolId == entry.Changed
                    ? FeatureImpactBasis.DirectCandidateMember : FeatureImpactBasis.StaticCallerPath;
                if (!unique.Add((feature.Id, member.SymbolId, entry.Changed, entry.Kind,
                        entry.Evidence.RelativeFile, entry.Evidence.Line, entry.Evidence.Column)))
                    continue;
                impacts.Add(new FeatureImpactSignal(feature.Id, member.SymbolId, entry.Changed,
                    entry.Kind, basis, entry.Evidence, path,
                    basis == FeatureImpactBasis.DirectCandidateMember
                        ? "Candidate feature member structurally changed; review required."
                        : "Member has a static source call path to structurally changed symbol; " +
                          "runtime execution and actual business impact are unknown."));
                if (impacts.Count > MaxImpacts)
                    throw new InvalidDataException("Too many candidate impacts for a bounded report.");
            }
        }
        return new FeatureImpactReport(Schema, before.Project, before.Revision, after.Revision,
            beforeCatalog.Revision, Scope, "Unknown", events.Count,
            impacts.OrderBy(i => i.FeatureId, StringComparer.Ordinal)
                .ThenBy(i => i.MemberSymbolId, StringComparer.Ordinal)
                .ThenBy(i => i.ChangedSymbolId, StringComparer.Ordinal)
                .ThenBy(i => i.ChangeKind).ThenBy(i => i.ChangeEvidence.RelativeFile, StringComparer.Ordinal)
                .ThenBy(i => i.ChangeEvidence.Line).ToArray());
    }

    private sealed record ChangeEvent(
        string Changed, StructuralChangeKind Kind, SourceEvidence Evidence, GraphSnapshot Graph);

    private static Dictionary<string, IReadOnlyList<GraphEdge>> BuildReverseCallGraph(
        GraphSnapshot graph) =>
        graph.Edges.Where(e => e.Relation == "Calls" &&
                e.Evidence.Kind is EvidenceKind.Static or EvidenceKind.Verified)
            .GroupBy(e => e.ToSymbolId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyList<GraphEdge>)g.OrderBy(e => e.EdgeId, StringComparer.Ordinal)
                    .ToArray(), StringComparer.Ordinal);

    /// <summary>
    /// Directed witness path member->...->changed, discovered from changed backwards.
    /// Retains exact edge identities and source evidence; never follows DependsOn,
    /// Inferred, Designed or Observed-only edges as a static call witness.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<GraphEdge>> ReversePaths(
        Dictionary<string, IReadOnlyList<GraphEdge>> reverse,
        string changed, int maxHops)
    {
        var paths = new Dictionary<string, IReadOnlyList<GraphEdge>>(StringComparer.Ordinal)
        {
            [changed] = []
        };
        var queue = new Queue<(string Symbol, int Depth)>();
        queue.Enqueue((changed, 0));
        while (queue.Count > 0)
        {
            var (symbol, depth) = queue.Dequeue();
            if (depth >= maxHops || !reverse.TryGetValue(symbol, out var incoming))
                continue;
            foreach (var edge in incoming)
            {
                if (paths.ContainsKey(edge.FromSymbolId)) continue;
                var suffix = paths[symbol];
                var path = new GraphEdge[suffix.Count + 1];
                path[0] = edge;
                for (int index = 0; index < suffix.Count; index++)
                    path[index + 1] = suffix[index];
                paths.Add(edge.FromSymbolId, path);
                queue.Enqueue((edge.FromSymbolId, depth + 1));
                if (paths.Count > MaxGraphNodes)
                    throw new InvalidDataException("Feature-impact traversal node cap exceeded.");
            }
        }
        return paths;
    }
}
