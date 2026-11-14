// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

public static class GraphQueries
{
    public static IReadOnlyList<GraphEdge> Neighbors(GraphSnapshot snapshot, string symbolId)
    {
        GraphSnapshotStore.Validate(snapshot);
        if (!snapshot.Nodes.Any(n => n.SymbolId == symbolId))
            throw new KeyNotFoundException($"Unknown symbol: {symbolId}");
        return snapshot.Edges.Where(e => e.FromSymbolId == symbolId || e.ToSymbolId == symbolId)
            .OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Bounded, stable, exclusive EdgeId-keyset pagination over incident graph edges.
    /// Scans the stored snapshot but retains at most pageSize + 1 edges; it does not
    /// claim bounded graph loading, a cached index, or permission-aware ViewScope.
    /// </summary>
    public static GraphNeighborPage NeighborsPage(
        GraphSnapshot snapshot, string symbolId, int pageSize = 50, string? afterEdgeId = null)
    {
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be 1..100.");
        if (string.IsNullOrWhiteSpace(symbolId) || symbolId.Length > 2048)
            throw new ArgumentException("An exact bounded symbol is required.", nameof(symbolId));
        if (afterEdgeId is not null && (afterEdgeId.Length == 0 || afterEdgeId.Length > 2048))
            throw new ArgumentException("An exact bounded edge cursor is required.", nameof(afterEdgeId));

        GraphSnapshotStore.Validate(snapshot);
        if (!snapshot.Nodes.Any(n => n.SymbolId == symbolId))
            throw new KeyNotFoundException($"Unknown symbol: {symbolId}");

        var cursorPresent = afterEdgeId is null;
        var selected = new SortedSet<GraphEdge>(Comparer<GraphEdge>.Create(
            (a, b) => StringComparer.Ordinal.Compare(a.EdgeId, b.EdgeId)));
        foreach (var edge in snapshot.Edges)
        {
            if (edge.FromSymbolId != symbolId && edge.ToSymbolId != symbolId) continue;
            if (edge.EdgeId == afterEdgeId)
            {
                cursorPresent = true;
                continue;
            }
            if (afterEdgeId is not null &&
                StringComparer.Ordinal.Compare(edge.EdgeId, afterEdgeId) <= 0) continue;

            selected.Add(edge);
            if (selected.Count > pageSize + 1)
                selected.Remove(selected.Max!);
        }
        if (!cursorPresent)
            throw new ArgumentException("Cursor is not an incident edge in this exact graph.", nameof(afterEdgeId));
        bool hasMore = selected.Count > pageSize;
        var page = selected.Take(pageSize).ToArray();
        return new GraphNeighborPage(GraphNeighborPage.ContractSchema, snapshot.Project, snapshot.Revision,
            symbolId, pageSize, page, hasMore, hasMore ? page[^1].EdgeId : null);
    }

    /// <summary>
    /// Bounded exact-revision enumeration of canonical (single-parent) children.
    /// Null parent selects graph roots; all results retain their source evidence.
    /// A scan-based prototype, not a precomputed hierarchy index or ViewScope.
    /// </summary>
    public static GraphChildPage ChildrenPage(
        GraphSnapshot snapshot, string? parentSymbolId = null,
        int pageSize = 50, string? afterSymbolId = null)
    {
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be 1..100.");
        if (parentSymbolId is not null &&
            (string.IsNullOrWhiteSpace(parentSymbolId) || parentSymbolId.Length > 2048))
            throw new ArgumentException("An exact bounded parent SymbolId is required.",
                nameof(parentSymbolId));
        if (afterSymbolId is not null &&
            (string.IsNullOrWhiteSpace(afterSymbolId) || afterSymbolId.Length > 2048))
            throw new ArgumentException("An exact bounded child cursor is required.",
                nameof(afterSymbolId));

        GraphSnapshotStore.Validate(snapshot);
        if (parentSymbolId is not null &&
            !snapshot.Nodes.Any(node => node.SymbolId == parentSymbolId))
            throw new KeyNotFoundException("Parent symbol is not in this exact graph.");

        bool cursorPresent = afterSymbolId is null;
        var selected = new SortedSet<GraphNode>(Comparer<GraphNode>.Create(
            (a, b) => StringComparer.Ordinal.Compare(a.SymbolId, b.SymbolId)));
        foreach (var node in snapshot.Nodes)
        {
            if (!string.Equals(node.ParentSymbolId, parentSymbolId, StringComparison.Ordinal))
                continue;
            if (string.Equals(node.SymbolId, afterSymbolId, StringComparison.Ordinal))
            {
                cursorPresent = true;
                continue;
            }
            if (afterSymbolId is not null &&
                StringComparer.Ordinal.Compare(node.SymbolId, afterSymbolId) <= 0)
                continue;
            selected.Add(node);
            if (selected.Count > pageSize + 1)
                selected.Remove(selected.Max!);
        }

        if (!cursorPresent)
            throw new ArgumentException("Cursor is not a direct child of the selected parent.",
                nameof(afterSymbolId));

        bool hasMore = selected.Count > pageSize;
        var page = selected.Take(pageSize).ToArray();
        return new GraphChildPage(GraphChildPage.ContractSchema, snapshot.Project,
            snapshot.Revision, parentSymbolId, pageSize, page, hasMore,
            hasMore ? page[^1].SymbolId : null);
    }

    /// <summary>
    /// Shortest path through source-proven static or independently verified directional edges.
    /// This is not a measured runtime call trace.
    /// </summary>
    public static IReadOnlyList<GraphEdge> ShortestStaticPath(
        GraphSnapshot snapshot, string from, string to, int maxVisited = 10000)
    {
        GraphSnapshotStore.Validate(snapshot);
        if (maxVisited < 1) throw new ArgumentOutOfRangeException(nameof(maxVisited));
        var ids = snapshot.Nodes.Select(n => n.SymbolId).ToHashSet(StringComparer.Ordinal);
        if (!ids.Contains(from) || !ids.Contains(to))
            throw new KeyNotFoundException("Source or destination symbol was not found.");
        if (from == to) return [];

        var edges = snapshot.Edges
            .Where(e => e.Evidence.Kind is EvidenceKind.Static or EvidenceKind.Verified)
            .GroupBy(e => e.FromSymbolId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var queue = new Queue<string>();
        var paths = new Dictionary<string, (string Prev, GraphEdge Edge)>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { from };
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var source = queue.Dequeue();
            if (!edges.TryGetValue(source, out var candidates)) continue;
            foreach (var edge in candidates)
            {
                if (!visited.Add(edge.ToSymbolId)) continue;
                if (visited.Count > maxVisited)
                    throw new InvalidOperationException("Graph traversal limit exceeded.");
                paths[edge.ToSymbolId] = (source, edge);
                if (edge.ToSymbolId == to)
                {
                    var route = new List<GraphEdge>();
                    string cursor = to;
                    while (cursor != from)
                    {
                        var item = paths[cursor];
                        route.Add(item.Edge);
                        cursor = item.Prev;
                    }
                    route.Reverse();
                    return route;
                }
                queue.Enqueue(edge.ToSymbolId);
            }
        }
        return [];
    }
}

/// <summary>
/// Direct single-parent ownership only, with a root scope when ParentSymbolId is null.
/// Cursor is an unsigned exact SymbolId, not an authorization token.
/// </summary>
public sealed record GraphChildPage(
    string Schema, string Project, string Revision, string? ParentSymbolId,
    int PageSize, IReadOnlyList<GraphNode> Nodes, bool HasMore, string? NextAfterSymbolId)
{
    public const string ContractSchema = "archpulse.graph-children-page/v1";
}

/// <summary>
/// Cursor is the previous page's exact last EdgeId; not an authenticated token.
/// Consumers must retain the same project, revision and symbol between pages.
/// </summary>
public sealed record GraphNeighborPage(
    string Schema, string Project, string Revision, string SymbolId, int PageSize,
    IReadOnlyList<GraphEdge> Edges, bool HasMore, string? NextAfterEdgeId)
{
    public const string ContractSchema = "archpulse.graph-neighbors-page/v1";
}
