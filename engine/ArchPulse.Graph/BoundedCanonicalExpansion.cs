// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

/// <summary>
/// Local-only, bounded canonical BFS. Never an authorization decision or
/// a general graph/interaction traversal. No continuation is promised.
/// </summary>
public static class BoundedCanonicalExpansion
{
    public static LocalCanonicalExpansion Execute(
        GraphSnapshot graph, LocalViewScopeDescriptor request)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Schema != LocalViewScopeDescriptor.ContractSchema ||
            request.AccessStatus != LocalViewScopeDescriptor.UncheckedAccess ||
            request.Project != graph.Project ||
            request.GraphRevision != graph.Revision)
            throw new ArgumentException("Descriptor does not bind to this exact graph.", nameof(request));
        var validated = LocalViewScopeDescriptor.Create(graph, request.RootSymbolId,
            request.Lens, request.MaxNodes, request.MaxEdges, request.Depth);
        if (validated != request)
            throw new ArgumentException("Descriptor is not canonical.", nameof(request));

        var children = graph.Nodes.Where(n => n.ParentSymbolId is not null)
            .GroupBy(n => n.ParentSymbolId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.SymbolId, StringComparer.Ordinal)
                .ToArray(), StringComparer.Ordinal);
        var ids = graph.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        var selected = new List<GraphNode>(Math.Min(request.MaxNodes, graph.Nodes.Count));
        var pending = new Queue<(string Id, int Depth)>();
        pending.Enqueue((request.RootSymbolId, 0));
        bool omittedCandidates = false;
        while (pending.Count != 0 && selected.Count < request.MaxNodes)
        {
            var item = pending.Dequeue();
            selected.Add(ids[item.Id]);
            if (item.Depth >= request.Depth ||
                !children.TryGetValue(item.Id, out var directChildren)) continue;
            foreach (var child in directChildren)
            {
                if (pending.Count + selected.Count >= request.MaxNodes + 1)
                {
                    omittedCandidates = true;
                    break;
                }
                pending.Enqueue((child.SymbolId, item.Depth + 1));
            }
        }

        // Exactly one canonical parent per child; emit at most MaxEdges
        // source-backed containment links for already selected endpoints.
        var selectedIds = selected.Select(n => n.SymbolId).ToHashSet(StringComparer.Ordinal);
        var internalParents = selected.Where(n => n.ParentSymbolId is not null &&
            selectedIds.Contains(n.ParentSymbolId)).ToArray();
        var links = internalParents.Take(request.MaxEdges).Select(n =>
            new LocalCanonicalParentLink(n.ParentSymbolId!, n.SymbolId, n.Evidence)).ToArray();
        bool truncatedNodes = pending.Count > 0 || omittedCandidates;
        bool truncatedLinks = internalParents.Length > links.Length;
        return new LocalCanonicalExpansion(LocalCanonicalExpansion.ContractSchema,
            graph.Project, graph.Revision, request.RootSymbolId, request.Lens,
            LocalViewScopeDescriptor.UncheckedAccess, selected.ToArray(), links,
            truncatedNodes || truncatedLinks, truncatedNodes, truncatedLinks,
            "NoContinuationAvailable");
    }
}

public sealed record LocalCanonicalParentLink(
    string ParentSymbolId, string ChildSymbolId, SourceEvidence Evidence);

public sealed record LocalCanonicalExpansion(
    string Schema, string Project, string Revision, string RootSymbolId,
    string Lens, string AccessStatus, IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<LocalCanonicalParentLink> CanonicalLinks, bool Partial,
    bool NodeBudgetReached, bool EdgeBudgetReached, string ContinuationStatus)
{
    public const string ContractSchema = "archpulse.local-canonical-expansion/v1";
}
