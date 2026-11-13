// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArchPulse.Graph;

/// <summary>
/// An explicit business-feature overlay. Membership is a *human proposal*, not an
/// observed call, ownership-parent relationship, test result or proven business flow.
/// </summary>
public enum FeatureCriticality { P0, P1, P2 }

public sealed record FeatureMembership(string SymbolId, string Reason, EvidenceKind Claim);

public sealed record BusinessFeature(
    string Id, string Title, string Description, string Owner,
    FeatureCriticality Criticality,
    IReadOnlyList<string> Scenarios,
    IReadOnlyList<string> AcceptanceContracts,
    IReadOnlyList<FeatureMembership> Members);

public sealed record FeatureCatalog(
    string Schema, string Project, string Revision,
    IReadOnlyList<BusinessFeature> Features);

public sealed record FeatureScope(
    string Schema, string Project, string Revision, BusinessFeature Feature,
    IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> StaticEdges,
    string MembershipScope, string ExecutionStatus, bool ObservedCoverage);

public static partial class FeatureAtlas
{
    public const string Schema = "archpulse.feature-catalog/v1";
    public const string ScopeSchema = "archpulse.feature-scope/v1";

    [GeneratedRegex("^[a-z][a-z0-9-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex FeatureIdPattern();

    public static void Validate(GraphSnapshot graph, FeatureCatalog catalog)
    {
        GraphSnapshotStore.Validate(graph);
        if (catalog is null || catalog.Schema != Schema ||
            catalog.Project != graph.Project || catalog.Revision != graph.Revision)
            throw new InvalidDataException("Feature catalog schema or exact graph identity differs.");
        if (catalog.Features is null || catalog.Features.Count is < 1 or > 500)
            throw new InvalidDataException("Feature catalog requires between 1 and 500 definitions.");

        var symbols = graph.Nodes.Select(n => n.SymbolId).ToHashSet(StringComparer.Ordinal);
        var featureIds = new HashSet<string>(StringComparer.Ordinal);
        var totalMembers = 0;
        foreach (var feature in catalog.Features)
        {
            if (feature is null || feature.Id is null || !FeatureIdPattern().IsMatch(feature.Id) ||
                !featureIds.Add(feature.Id))
                throw new InvalidDataException("Feature IDs must be unique lowercase slug identifiers.");
            ValidateText(feature.Title, 160, "Feature title");
            ValidateText(feature.Description, 2048, "Feature description");
            ValidateText(feature.Owner, 160, "Feature owner");
            if (!Enum.IsDefined(feature.Criticality))
                throw new InvalidDataException("Unknown feature criticality.");
            ValidateStatements(feature.Scenarios, "Scenarios");
            ValidateStatements(feature.AcceptanceContracts, "Acceptance contracts");
            if (feature.Members is null || feature.Members.Count is < 1 or > 500)
                throw new InvalidDataException("A feature requires 1-500 explicit graph members.");
            totalMembers += feature.Members.Count;
            if (totalMembers > 5000)
                throw new InvalidDataException("Feature catalog exceeds 5000 total members.");

            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in feature.Members)
            {
                if (member is null || string.IsNullOrWhiteSpace(member.SymbolId) ||
                    !symbols.Contains(member.SymbolId) || !members.Add(member.SymbolId))
                    throw new InvalidDataException("Feature member is missing, duplicated or absent from exact graph.");
                ValidateText(member.Reason, 1024, "Feature membership rationale");
                if (member.Claim is not (EvidenceKind.Designed or EvidenceKind.Inferred))
                    throw new InvalidDataException("Manual business membership cannot claim source, observed or verified evidence.");
            }
        }
    }

    public static FeatureScope Project(GraphSnapshot graph, FeatureCatalog catalog, string id)
    {
        Validate(graph, catalog);
        var feature = catalog.Features.SingleOrDefault(f => f.Id == id)
            ?? throw new KeyNotFoundException("Exact feature ID was not found.");
        var memberIds = feature.Members.Select(m => m.SymbolId).ToHashSet(StringComparer.Ordinal);
        var nodes = graph.Nodes.Where(n => memberIds.Contains(n.SymbolId))
            .OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray();
        var edges = graph.Edges.Where(e => memberIds.Contains(e.FromSymbolId) &&
                 memberIds.Contains(e.ToSymbolId) &&
                 e.Evidence.Kind is EvidenceKind.Static or EvidenceKind.Verified)
            .OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray();
        return new FeatureScope(ScopeSchema, graph.Project, graph.Revision, feature,
            nodes, edges, "ManualCandidateMembership;StaticInternalGraphEdgesOnly",
            "Unknown", false);
    }

    private static void ValidateStatements(IReadOnlyList<string> values, string name)
    {
        if (values is null || values.Count > 100)
            throw new InvalidDataException($"{name} must contain at most 100 statements.");
        foreach (var value in values) ValidateText(value, 2048, name);
    }

    private static void ValidateText(string text, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength || text.Any(char.IsControl))
            throw new InvalidDataException($"{field} requires safe, nonempty bounded text.");
    }
}

/// <summary>
/// Immutable, local-only revisioned sidecar; a different byte sequence at the same
/// project/revision key is rejected instead of overwriting prior human proposals.
/// </summary>
public sealed class FeatureCatalogStore
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public FeatureCatalogStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Store directory required.", nameof(directory));
        _directory = Path.GetFullPath(directory);
    }

    public static FeatureCatalog Parse(string contents)
    {
        if (string.IsNullOrWhiteSpace(contents) || Encoding.UTF8.GetByteCount(contents) > 2_000_000)
            throw new InvalidDataException("Feature manifest is empty or oversized.");
        return JsonSerializer.Deserialize<FeatureCatalog>(contents, JsonOptions)
            ?? throw new InvalidDataException("Feature manifest is empty.");
    }

    public string Save(GraphSnapshot graph, FeatureCatalog catalog)
    {
        FeatureAtlas.Validate(graph, catalog);
        Directory.CreateDirectory(_directory);
        var path = PathFor(graph.Project, graph.Revision);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, JsonOptions);
        if (File.Exists(path))
        {
            EnsureSame(path, bytes);
            return path;
        }
        var temp = Path.Combine(_directory, $".archpulse-feature-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            try { File.Move(temp, path, overwrite: false); }
            catch (IOException) when (File.Exists(path)) { EnsureSame(path, bytes); }
            return path;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public FeatureCatalog? Load(GraphSnapshot graph)
    {
        GraphSnapshotStore.Validate(graph);
        var path = PathFor(graph.Project, graph.Revision);
        if (!File.Exists(path)) return null;
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 2_000_000)
            throw new InvalidDataException("Stored feature catalog exceeds size limit.");
        var catalog = JsonSerializer.Deserialize<FeatureCatalog>(file, JsonOptions)
            ?? throw new InvalidDataException("Stored feature catalog is empty.");
        FeatureAtlas.Validate(graph, catalog);
        return catalog;
    }

    private string PathFor(string project, string revision)
    {
        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("Catalog project and revision required.");
        var key = Encoding.UTF8.GetBytes("feature-v1\0" + project + "\0" + revision);
        return Path.Combine(_directory,
            Convert.ToHexStringLower(SHA256.HashData(key)) + ".features.json");
    }

    private static void EnsureSame(string path, byte[] expected)
    {
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
            throw new InvalidOperationException(
                "A different feature catalog already exists for this immutable revision.");
    }
}
