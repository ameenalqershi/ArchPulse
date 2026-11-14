// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

/// <summary>
/// An immutable, exact-revision, canonical-ownership projection. Containment
/// comes solely from ParentSymbolId, never from Calls, DependsOn, or feature
/// membership. Not a persisted index or a permission-aware ViewScope.
/// </summary>
public sealed class CanonicalHierarchyTopology
{
    public const int MaxBreadcrumbNodes = 128;

    private readonly GraphSnapshot _graph;
    private readonly Dictionary<string, GraphNode> _nodes;
    private readonly Dictionary<string, GraphNode[]> _children;
    private readonly Dictionary<string, int> _descendantCounts;
    private readonly GraphNode[] _roots;

    public CanonicalHierarchyTopology(GraphSnapshot graph)
    {
        GraphSnapshotStore.Validate(graph);
        _graph = graph;
        _nodes = graph.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        _roots = graph.Nodes.Where(n => n.ParentSymbolId is null)
            .OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray();

        _children = graph.Nodes.Where(n => n.ParentSymbolId is not null)
            .GroupBy(n => n.ParentSymbolId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => g.OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        // Iterative postorder: no recursive stack overflow on deep Roslyn
        // containment trees. Each node is counted once in its unique parent.
        var preorder = new List<GraphNode>(_nodes.Count);
        var pending = new Stack<GraphNode>(_roots.Reverse());
        while (pending.TryPop(out var node))
        {
            preorder.Add(node);
            if (_children.TryGetValue(node.SymbolId, out var children))
                foreach (var child in children.Reverse()) pending.Push(child);
        }
        _descendantCounts = new Dictionary<string, int>(_nodes.Count, StringComparer.Ordinal);
        for (int i = preorder.Count - 1; i >= 0; i--)
        {
            var node = preorder[i];
            int count = 0;
            if (_children.TryGetValue(node.SymbolId, out var children))
                foreach (var child in children)
                    count = checked(count + 1 + _descendantCounts[child.SymbolId]);
            _descendantCounts.Add(node.SymbolId, count);
        }
    }

    /// <summary>
    /// Exact source-backed breadcrumb, root identity, depth, direct child
    /// evidence modes and transitive containment count for one SymbolId.
    /// Does not claim any member has been observed or independently verified.
    /// </summary>
    public GraphScopeTopology Describe(string symbolId)
    {
        if (string.IsNullOrWhiteSpace(symbolId) || symbolId.Length > 2048)
            throw new ArgumentException("An exact bounded SymbolId is required.", nameof(symbolId));
        if (!_nodes.TryGetValue(symbolId, out var selected))
            throw new KeyNotFoundException("Unknown canonical graph symbol.");

        var trail = new List<GraphNode>();
        GraphNode? current = selected;
        while (current is not null)
        {
            if (trail.Count >= MaxBreadcrumbNodes)
                throw new InvalidOperationException("Canonical hierarchy breadcrumb budget exceeded.");
            trail.Add(current);
            current = current.ParentSymbolId is null ? null : _nodes[current.ParentSymbolId];
        }
        trail.Reverse();
        var children = _children.GetValueOrDefault(symbolId) ?? [];
        var evidence = Enum.GetValues<EvidenceKind>()
            .Select(kind => new GraphEvidenceModeCount(
                kind, children.Count(n => n.Evidence.Kind == kind))).ToArray();
        return new GraphScopeTopology(GraphScopeTopology.ContractSchema,
            _graph.Project, _graph.Revision, selected.SymbolId,
            trail[0].SymbolId, trail.Count - 1, selected, trail.ToArray(),
            children.Length, _descendantCounts[symbolId], evidence);
    }

    public IReadOnlyList<GraphNode> Roots => _roots;

    /// <summary>
    /// Memory-bounded child enumeration after a single validated index build.
    /// The cursor must identify an actual child in this exact revision/scope.
    /// </summary>
    public GraphChildPage ChildrenPage(string? parentSymbolId = null,
        int pageSize = 50, string? afterSymbolId = null)
    {
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (parentSymbolId is not null &&
            (string.IsNullOrWhiteSpace(parentSymbolId) || parentSymbolId.Length > 2048))
            throw new ArgumentException("Invalid parent SymbolId.", nameof(parentSymbolId));
        if (afterSymbolId is not null &&
            (string.IsNullOrWhiteSpace(afterSymbolId) || afterSymbolId.Length > 2048))
            throw new ArgumentException("Invalid child cursor.", nameof(afterSymbolId));
        if (parentSymbolId is not null && !_nodes.ContainsKey(parentSymbolId))
            throw new KeyNotFoundException("Unknown canonical graph parent.");

        var nodes = parentSymbolId is null ? _roots : _children.GetValueOrDefault(parentSymbolId) ?? [];
        int start = 0;
        if (afterSymbolId is not null)
        {
            int lo = 0;
            int hi = nodes.Length - 1;
            int found = -1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                int cmp = StringComparer.Ordinal.Compare(nodes[mid].SymbolId, afterSymbolId);
                if (cmp == 0) { found = mid; break; }
                if (cmp < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            if (found < 0)
                throw new ArgumentException("Cursor is not a direct child.", nameof(afterSymbolId));
            start = found + 1;
        }
        int count = Math.Min(pageSize, nodes.Length - start);
        var page = nodes.AsSpan(start, count).ToArray();
        bool hasMore = start + count < nodes.Length;
        return new GraphChildPage(GraphChildPage.ContractSchema,
            _graph.Project, _graph.Revision, parentSymbolId,
            pageSize, page, hasMore, hasMore ? page[^1].SymbolId : null);
    }
}

public sealed record GraphEvidenceModeCount(EvidenceKind Kind, int Count);

/// <summary>
/// Structural scope facts from a source revision, not runtime observations.
/// </summary>
public sealed record GraphScopeTopology(
    string Schema, string Project, string Revision, string SymbolId,
    string RootSymbolId, int Depth, GraphNode Node,
    IReadOnlyList<GraphNode> Breadcrumb,
    int DirectChildrenCount, int DescendantCount,
    IReadOnlyList<GraphEvidenceModeCount> DirectChildEvidence)
{
    public const string ContractSchema = "archpulse.graph-scope-topology/v1";
}
