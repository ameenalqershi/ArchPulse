// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

/// <summary>
/// Validated exact-revision local query selection, NOT an authorization token.
/// Its immutable values cannot grant visibility into a graph.
/// </summary>
public sealed record LocalViewScopeDescriptor(
    string Schema, string Project, string GraphRevision, string RootSymbolId,
    string Lens, int MaxNodes, int MaxEdges, int Depth, string AccessStatus)
{
    public const string ContractSchema = "archpulse.local-view-scope/v1";
    public const string UncheckedAccess = "AuthorizationNotEvaluated";

    private static readonly HashSet<string> AllowedLenses = new(
        ["architecture", "dependency", "feature", "data", "test", "impact"],
        StringComparer.Ordinal);

    public static LocalViewScopeDescriptor Create(
        GraphSnapshot snapshot, string rootSymbolId, string lens = "architecture",
        int maxNodes = 100, int maxEdges = 200, int depth = 1)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (string.IsNullOrWhiteSpace(rootSymbolId) || rootSymbolId.Length > 2048)
            throw new ArgumentException("Exact bounded scope ID required.", nameof(rootSymbolId));
        if (lens is null || !AllowedLenses.Contains(lens))
            throw new ArgumentException("Unsupported exact graph lens.", nameof(lens));
        if (maxNodes is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maxNodes));
        if (maxEdges is < 1 or > 2000) throw new ArgumentOutOfRangeException(nameof(maxEdges));
        if (depth is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(depth));

        GraphSnapshotStore.Validate(snapshot);
        if (!snapshot.Nodes.Any(node => string.Equals(
                node.SymbolId, rootSymbolId, StringComparison.Ordinal)))
            throw new KeyNotFoundException("Unknown exact canonical graph scope.");

        return new(ContractSchema, snapshot.Project, snapshot.Revision,
            rootSymbolId, lens, maxNodes, maxEdges, depth, UncheckedAccess);
    }
}
