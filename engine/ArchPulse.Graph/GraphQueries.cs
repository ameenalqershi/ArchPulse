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
