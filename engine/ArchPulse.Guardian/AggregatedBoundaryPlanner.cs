// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;

namespace ArchPulse.Guardian;

/// <summary>
/// Experimental, local-only and exact-revision edge bundling. Policy findings are
/// caller-supplied, not authenticated approval or proof of policy completeness.
/// Never use these summaries as an authorization or enforcement decision.
/// </summary>
public static class AggregatedBoundaryPlanner
{
    public const int MaxScanEdges = 100_000;
    public const int MaxGroups = 500;
    public const int MaxPageSize = 100;
    public const int WitnessPreviewSize = 20;
    public const string AccessStatus = "AuthorizationNotEvaluated";

    public static AggregatedBoundaryPlan Plan(GraphSnapshot graph, string scopeSymbolId,
        IReadOnlyCollection<ArchitectureFinding>? findings = null, int maxScanEdges = MaxScanEdges)
    {
        var boundaries = Index(graph, scopeSymbolId, maxScanEdges);
        var policy = ValidateFindings(graph, findings);
        var grouped = boundaries.GroupBy(w => w.Key)
            .OrderBy(g => g.Key.Direction, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Relation, StringComparer.Ordinal)
            .ThenBy(g => g.Key.ExternalScopeSymbolId, StringComparer.Ordinal)
            .Select(g =>
            {
                var entries = g.OrderBy(w => w.Edge.EdgeId, StringComparer.Ordinal).ToArray();
                var withFindings = entries.Select(w => (w.Edge.EdgeId,
                    Findings: policy.GetValueOrDefault(w.Edge.EdgeId) ?? [])).ToArray();
                var evidenceCounts = Enum.GetValues<EvidenceKind>().Select(kind =>
                    new GraphEvidenceModeCount(kind, entries.Count(w => w.Edge.Evidence.Kind == kind)))
                    .ToArray();
                return new AggregatedBoundaryEdge(
                    g.Key, entries.Length, evidenceCounts,
                    findings is null ? "NotEvaluated" : "CallerSuppliedFindingsUnverified",
                    findings is null ? null : withFindings.Count(x =>
                        x.Findings.Any(f => f.Outcome == RuleOutcome.Violation)),
                    findings is null ? null : withFindings.Count(x =>
                        x.Findings.Any(f => f.Outcome == RuleOutcome.Violation &&
                            f.Severity == RuleSeverity.Blocker)),
                    findings is null ? null : withFindings.Count(x =>
                        x.Findings.Any(f => f.Outcome == RuleOutcome.Waived)),
                    findings is null ? null : withFindings.Count(x =>
                        x.Findings.Any(f => f.Outcome == RuleOutcome.Unknown)),
                    entries.Take(WitnessPreviewSize).Select(w => w.Edge.EdgeId).ToArray(),
                    entries.Length > WitnessPreviewSize);
            }).ToArray();
        return new AggregatedBoundaryPlan(AggregatedBoundaryPlan.ContractSchema,
            graph.Project, graph.Revision, scopeSymbolId, AccessStatus,
            maxScanEdges, boundaries.Count, grouped,
            findings is null ? null : findings.Count(f => f.EdgeId is null),
            findings is null ? null : findings.Count(f =>
                f.EdgeId is null && f.Severity == RuleSeverity.Blocker),
            "NoAuthenticatedPolicyCoverageOrTenantPermissions");
    }

    /// <summary>
    /// Enumerates actual source edges in one bundle by stable EdgeId cursor.
    /// The cursor is local and untrusted; never use it as a signed access token.
    /// </summary>
    public static AggregatedBoundaryWitnessPage WitnessesPage(
        GraphSnapshot graph, string scopeSymbolId, AggregatedBoundaryKey key,
        int pageSize = 50, string? afterEdgeId = null, int maxScanEdges = MaxScanEdges)
    {
        if (pageSize is < 1 or > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (afterEdgeId is not null &&
            (string.IsNullOrWhiteSpace(afterEdgeId) || afterEdgeId.Length > 2048))
            throw new ArgumentException("Invalid local boundary witness cursor.", nameof(afterEdgeId));
        var entries = Index(graph, scopeSymbolId, maxScanEdges)
            .Where(w => w.Key == key).OrderBy(w => w.Edge.EdgeId, StringComparer.Ordinal).ToArray();
        if (entries.Length == 0)
            throw new KeyNotFoundException("Boundary bundle not found at this revision.");
        int start = 0;
        if (afterEdgeId is not null)
        {
            int found = Array.FindIndex(entries, w =>
                string.Equals(w.Edge.EdgeId, afterEdgeId, StringComparison.Ordinal));
            if (found == -1)
                throw new ArgumentException("Cursor is not a witness in this boundary bundle.",
                    nameof(afterEdgeId));
            start = found + 1;
        }
        var page = entries.Skip(start).Take(pageSize).Select(w => w.Edge).ToArray();
        bool hasMore = start + page.Length < entries.Length;
        return new AggregatedBoundaryWitnessPage(
            AggregatedBoundaryWitnessPage.ContractSchema, graph.Project, graph.Revision,
            scopeSymbolId, key, AccessStatus, entries.Length, page,
            hasMore, hasMore ? page[^1].EdgeId : null);
    }

    private static List<BoundaryWitness> Index(
        GraphSnapshot graph, string scopeSymbolId, int maxScanEdges)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (string.IsNullOrWhiteSpace(scopeSymbolId) || scopeSymbolId.Length > 2048)
            throw new ArgumentException("Exact bounded canonical scope required.", nameof(scopeSymbolId));
        if (maxScanEdges is < 1 or > MaxScanEdges)
            throw new ArgumentOutOfRangeException(nameof(maxScanEdges));
        if (graph.Edges is null || graph.Edges.Count > maxScanEdges)
            throw new InvalidOperationException("Boundary scan budget exceeded.");
        GraphSnapshotStore.Validate(graph);
        var nodes = graph.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        if (!nodes.ContainsKey(scopeSymbolId))
            throw new KeyNotFoundException("Canonical scope not present in the requested revision.");
        var children = graph.Nodes.Where(n => n.ParentSymbolId is not null)
            .ToLookup(n => n.ParentSymbolId!, n => n.SymbolId, StringComparer.Ordinal);
        var inside = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(scopeSymbolId);
        while (pending.TryPop(out var id))
        {
            if (!inside.Add(id)) continue;
            foreach (var child in children[id]) pending.Push(child);
        }
        string ExternalScope(string id)
        {
            var current = nodes[id];
            while (current.Kind != GraphNodeKind.Project && current.ParentSymbolId is not null)
                current = nodes[current.ParentSymbolId];
            return current.SymbolId;
        }
        var result = new List<BoundaryWitness>();
        var groupKeys = new HashSet<AggregatedBoundaryKey>();
        foreach (var edge in graph.Edges)
        {
            bool from = inside.Contains(edge.FromSymbolId);
            bool to = inside.Contains(edge.ToSymbolId);
            if (from == to) continue;
            string externalId = from ? edge.ToSymbolId : edge.FromSymbolId;
            var key = new AggregatedBoundaryKey(from ? "Outbound" : "Inbound",
                edge.Relation, ExternalScope(externalId));
            if (groupKeys.Add(key) && groupKeys.Count > MaxGroups)
                throw new InvalidOperationException("Boundary aggregation group budget exceeded.");
            result.Add(new BoundaryWitness(key, edge));
        }
        return result;
    }

    private static Dictionary<string, ArchitectureFinding[]> ValidateFindings(
        GraphSnapshot graph, IReadOnlyCollection<ArchitectureFinding>? findings)
    {
        var result = new Dictionary<string, ArchitectureFinding[]>(StringComparer.Ordinal);
        if (findings is null) return result;
        var edges = graph.Edges.ToDictionary(e => e.EdgeId, StringComparer.Ordinal);
        foreach (var f in findings)
        {
            if (f is null)
                throw new ArgumentException("Null policy finding is not allowed.", nameof(findings));
            if (f.EdgeId is null) continue; // No edge-specific claim; do not fabricate one.
            if (!edges.TryGetValue(f.EdgeId, out var source) ||
                f.FromSymbolId != source.FromSymbolId ||
                f.ToSymbolId != source.ToSymbolId ||
                f.Evidence != source.Evidence ||
                string.IsNullOrWhiteSpace(f.RuleId) || string.IsNullOrWhiteSpace(f.RuleVersion) ||
                !Enum.IsDefined(f.Severity) || !Enum.IsDefined(f.Outcome))
                throw new ArgumentException("Policy finding does not bind to an exact source edge.",
                    nameof(findings));
        }
        foreach (var g in findings.Where(f => f.EdgeId is not null).GroupBy(f => f.EdgeId!,
            StringComparer.Ordinal))
            result.Add(g.Key, g.ToArray());
        return result;
    }

    private sealed record BoundaryWitness(AggregatedBoundaryKey Key, GraphEdge Edge);
}

public sealed record AggregatedBoundaryKey(
    string Direction, string Relation, string ExternalScopeSymbolId);

public sealed record AggregatedBoundaryEdge(
    AggregatedBoundaryKey Key, int SourceEdgeCount,
    IReadOnlyList<GraphEvidenceModeCount> EvidenceCounts,
    string PolicyEvidenceStatus, int? ViolationEdgeCount, int? BlockingViolationEdgeCount,
    int? WaivedEdgeCount, int? UnknownPolicyEdgeCount,
    IReadOnlyList<string> PreviewWitnessEdgeIds, bool HasMoreWitnesses);

public sealed record AggregatedBoundaryPlan(
    string Schema, string Project, string Revision, string ScopeSymbolId,
    string AccessStatus, int MaxScanEdges, int TotalBoundaryEdges,
    IReadOnlyList<AggregatedBoundaryEdge> Bundles,
    int? UnscopedPolicyFindingsCount, int? UnscopedBlockerFindingsCount,
    string TrustLimitation)
{
    public const string ContractSchema = "archpulse.local-aggregated-boundaries/v1";
}

public sealed record AggregatedBoundaryWitnessPage(
    string Schema, string Project, string Revision, string ScopeSymbolId,
    AggregatedBoundaryKey Key, string AccessStatus, int TotalWitnesses,
    IReadOnlyList<GraphEdge> Witnesses, bool HasMore, string? NextAfterEdgeId)
{
    public const string ContractSchema = "archpulse.local-aggregated-boundary-witnesses/v1";
}
