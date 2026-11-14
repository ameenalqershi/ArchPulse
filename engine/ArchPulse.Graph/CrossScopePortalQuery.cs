// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

/// <summary>
/// Local read-only boundary projection over one exact source GraphSnapshot.
/// ParentSymbolId alone defines the canonical scope subtree. Every returned
/// portal is backed by an original GraphEdge; feature membership is NOT
/// inferred and authorization has NOT been evaluated.
/// </summary>
public static class CrossScopePortalQuery
{
    public const int MaxPageSize = 100;
    public const int MaxScanBudget = 1_000_000;
    public const string AccessStatus = "AuthorizationNotEvaluated";
    public const string MembershipStatus = "NotIndexed";

    public static CrossScopePortalPage GetPage(
        GraphSnapshot graph, string scopeSymbolId, int pageSize = 50,
        string? afterEdgeId = null, int maxScanEdges = 100_000)
    {
        if (string.IsNullOrWhiteSpace(scopeSymbolId) || scopeSymbolId.Length > 2048)
            throw new ArgumentException("Exact bounded canonical scope is required.", nameof(scopeSymbolId));
        if (pageSize is < 1 or > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (afterEdgeId is not null &&
            (string.IsNullOrWhiteSpace(afterEdgeId) || afterEdgeId.Length > 2048))
            throw new ArgumentException("Exact bounded edge cursor is required.", nameof(afterEdgeId));
        if (maxScanEdges is < 1 or > MaxScanBudget)
            throw new ArgumentOutOfRangeException(nameof(maxScanEdges));

        // Never return a partial answer because an explicit scan budget was
        // exceeded: it could otherwise silently hide a cross-scope relation.
        if (graph.Edges is null || graph.Edges.Count > maxScanEdges)
            throw new InvalidOperationException("Graph portal scan budget exceeded.");
        GraphSnapshotStore.Validate(graph);
        var nodes = graph.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        if (!nodes.ContainsKey(scopeSymbolId))
            throw new KeyNotFoundException("Canonical scope not found in the exact revision.");

        var children = graph.Nodes.Where(n => n.ParentSymbolId is not null)
            .GroupBy(n => n.ParentSymbolId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(n => n.SymbolId).ToArray(),
                StringComparer.Ordinal);
        var inside = new HashSet<string>(StringComparer.Ordinal) { scopeSymbolId };
        var pending = new Stack<string>();
        pending.Push(scopeSymbolId);
        while (pending.TryPop(out string? parent))
        {
            if (!children.TryGetValue(parent, out var descendants)) continue;
            foreach (string child in descendants)
                if (inside.Add(child)) pending.Push(child);
        }

        var selected = new SortedSet<GraphEdge>(Comparer<GraphEdge>.Create(
            (a, b) => StringComparer.Ordinal.Compare(a.EdgeId, b.EdgeId)));
        bool cursorPresent = afterEdgeId is null;
        int total = 0;
        var directionCounts = new int[2];
        var evidenceCounts = new int[Enum.GetValues<EvidenceKind>().Length];
        foreach (var edge in graph.Edges)
        {
            bool fromInside = inside.Contains(edge.FromSymbolId);
            bool toInside = inside.Contains(edge.ToSymbolId);
            if (fromInside == toInside) continue; // Both internal or both external.
            total++;
            directionCounts[fromInside ? 0 : 1]++;
            evidenceCounts[(int)edge.Evidence.Kind]++;

            if (string.Equals(edge.EdgeId, afterEdgeId, StringComparison.Ordinal))
            {
                cursorPresent = true;
                continue;
            }
            if (afterEdgeId is not null &&
                StringComparer.Ordinal.Compare(edge.EdgeId, afterEdgeId) <= 0)
                continue;
            selected.Add(edge);
            if (selected.Count > pageSize + 1)
                selected.Remove(selected.Max!);
        }
        if (!cursorPresent)
            throw new ArgumentException(
                "Cursor does not identify a boundary edge in the selected scope.",
                nameof(afterEdgeId));

        bool hasMore = selected.Count > pageSize;
        var edges = selected.Take(pageSize).ToArray();
        var portals = edges.Select(edge =>
        {
            bool outbound = inside.Contains(edge.FromSymbolId);
            string internalId = outbound ? edge.FromSymbolId : edge.ToSymbolId;
            string externalId = outbound ? edge.ToSymbolId : edge.FromSymbolId;
            return new CrossScopePortal(edge.EdgeId,
                outbound ? "Outbound" : "Inbound",
                edge.Relation, nodes[internalId], nodes[externalId], edge);
        }).ToArray();
        var modes = Enum.GetValues<EvidenceKind>().Select(kind =>
            new GraphEvidenceModeCount(kind, evidenceCounts[(int)kind])).ToArray();
        return new CrossScopePortalPage(CrossScopePortalPage.ContractSchema,
            graph.Project, graph.Revision, scopeSymbolId, AccessStatus,
            MembershipStatus, pageSize, maxScanEdges, total,
            directionCounts[0], directionCounts[1], modes, portals,
            hasMore, hasMore ? portals[^1].EdgeId : null);
    }
}

/// <summary>
/// A directional boundary crossing, not a duplicate canonical owner.
/// The original GraphEdge and both endpoint SourceEvidence records are retained.
/// </summary>
public sealed record CrossScopePortal(
    string EdgeId, string Direction, string Relation,
    GraphNode InternalNode, GraphNode ExternalNode, GraphEdge Edge);

/// <summary>
/// Aggregates reflect source evidence categories, not verified user permission
/// or a claim of observed runtime execution.
/// </summary>
public sealed record CrossScopePortalPage(
    string Schema, string Project, string Revision, string ScopeSymbolId,
    string AccessStatus, string FeatureMembershipStatus,
    int PageSize, int MaxScanEdges, int TotalCrossScopeEdges,
    int OutboundCount, int InboundCount,
    IReadOnlyList<GraphEvidenceModeCount> EvidenceCounts,
    IReadOnlyList<CrossScopePortal> Portals, bool HasMore,
    string? NextAfterEdgeId)
{
    public const string ContractSchema = "archpulse.graph-cross-scope-portals/v1";
}
