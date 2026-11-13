// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchPulse.Graph;

/// <summary>
/// Local-first immutable graph snapshots. One exact revision and project identity maps to
/// one content-validated document; partial writes are not visible to readers.
/// This is a single-process/local-volume first slice, not a distributed transaction engine.
/// </summary>
public sealed class GraphSnapshotStore
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public GraphSnapshotStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Store directory is required.", nameof(directory));
        _directory = Path.GetFullPath(directory);
    }

    public string Save(GraphSnapshot snapshot)
    {
        Validate(snapshot);
        Directory.CreateDirectory(_directory);
        string path = GetPath(snapshot.Project, snapshot.Revision);
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        if (File.Exists(path))
        {
            EnsureSameBytes(path, data);
            return path;
        }

        string temp = Path.Combine(_directory, $".archpulse-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            try
            {
                File.Move(temp, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                EnsureSameBytes(path, data); // Idempotent retry after concurrent creation.
            }
            return path;
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public GraphSnapshot? Load(string project, string revision)
    {
        string path = GetPath(project, revision);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        var snapshot = JsonSerializer.Deserialize<GraphSnapshot>(stream, JsonOptions)
            ?? throw new InvalidDataException("Graph snapshot is empty.");
        Validate(snapshot);
        if (snapshot.Project != project || snapshot.Revision != revision)
            throw new InvalidDataException("Stored snapshot has a mismatched identity or revision.");
        return snapshot;
    }

    public static GraphDifference Compare(GraphSnapshot previous, GraphSnapshot current)
    {
        Validate(previous);
        Validate(current);
        if (!string.Equals(previous.Project, current.Project, StringComparison.Ordinal))
            throw new InvalidOperationException("Cannot compare graph snapshots from different projects.");
        var oldNodes = previous.Nodes.ToDictionary(node => node.SymbolId, StringComparer.Ordinal);
        var newNodes = current.Nodes.ToDictionary(node => node.SymbolId, StringComparer.Ordinal);
        var oldEdges = previous.Edges.ToDictionary(edge => edge.EdgeId, StringComparer.Ordinal);
        var newEdges = current.Edges.ToDictionary(edge => edge.EdgeId, StringComparer.Ordinal);
        return new GraphDifference(previous.Revision, current.Revision,
            newNodes.Keys.Except(oldNodes.Keys).Order(StringComparer.Ordinal).ToArray(),
            oldNodes.Keys.Except(newNodes.Keys).Order(StringComparer.Ordinal).ToArray(),
            newEdges.Keys.Except(oldEdges.Keys).Order(StringComparer.Ordinal).ToArray(),
            oldEdges.Keys.Except(newEdges.Keys).Order(StringComparer.Ordinal).ToArray(),
            oldNodes.Keys.Intersect(newNodes.Keys)
                .Where(id => !SameNodeAcrossRevisions(oldNodes[id], newNodes[id]))
                .Order(StringComparer.Ordinal).ToArray(),
            oldEdges.Keys.Intersect(newEdges.Keys)
                .Where(id => !SameEdgeAcrossRevisions(oldEdges[id], newEdges[id]))
                .Order(StringComparer.Ordinal).ToArray());
    }

    // Source revision is intentionally ignored: every static evidence record
    // receives the new revision even when all other content is unchanged.
    // This is a source-location/structural delta, not an AST/body diff.
    private static bool SameLocationAcrossRevisions(SourceEvidence old, SourceEvidence current) =>
        old.Kind == current.Kind &&
        string.Equals(old.RelativeFile, current.RelativeFile, StringComparison.Ordinal) &&
        old.Line == current.Line && old.Column == current.Column;

    private static bool SameNodeAcrossRevisions(GraphNode old, GraphNode current) =>
        old.Kind == current.Kind &&
        string.Equals(old.Label, current.Label, StringComparison.Ordinal) &&
        string.Equals(old.ParentSymbolId, current.ParentSymbolId, StringComparison.Ordinal) &&
        SameLocationAcrossRevisions(old.Evidence, current.Evidence);

    private static bool SameEdgeAcrossRevisions(GraphEdge old, GraphEdge current) =>
        string.Equals(old.FromSymbolId, current.FromSymbolId, StringComparison.Ordinal) &&
        string.Equals(old.ToSymbolId, current.ToSymbolId, StringComparison.Ordinal) &&
        string.Equals(old.Relation, current.Relation, StringComparison.Ordinal) &&
        SameLocationAcrossRevisions(old.Evidence, current.Evidence);

    public static void Validate(GraphSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Project) || string.IsNullOrWhiteSpace(snapshot.Revision))
            throw new InvalidDataException("Graph identity and revision are required.");
        if (snapshot.Nodes is null || snapshot.Edges is null || snapshot.Diagnostics is null)
            throw new InvalidDataException("Required graph collections are missing.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in snapshot.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.SymbolId) || !ids.Add(node.SymbolId))
                throw new InvalidDataException("Duplicate or empty graph node ID.");
            ValidateEvidence(node.Evidence, snapshot.Revision);
        }
        foreach (var node in snapshot.Nodes)
        {
            if (node.ParentSymbolId is not null && !ids.Contains(node.ParentSymbolId))
                throw new InvalidDataException($"Orphan parent of {node.SymbolId}.");
            if (node.ParentSymbolId == node.SymbolId)
                throw new InvalidDataException("Graph node cannot own itself.");
        }

        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in snapshot.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.EdgeId) || !edgeIds.Add(edge.EdgeId))
                throw new InvalidDataException("Duplicate or empty graph edge ID.");
            if (!ids.Contains(edge.FromSymbolId) || !ids.Contains(edge.ToSymbolId))
                throw new InvalidDataException($"Orphan relation: {edge.EdgeId}.");
            if (string.IsNullOrWhiteSpace(edge.Relation))
                throw new InvalidDataException("Graph relation is missing.");
            ValidateEvidence(edge.Evidence, snapshot.Revision);
        }

        // Validate the canonical parent hierarchy independently from call/flow edges.
        var parents = snapshot.Nodes.ToDictionary(n => n.SymbolId, n => n.ParentSymbolId,
            StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string? cursor = id;
            while (cursor is not null)
            {
                if (!visited.Add(cursor))
                    throw new InvalidDataException($"Cycle in canonical ownership hierarchy at {cursor}.");
                cursor = parents[cursor];
            }
        }
    }

    private static void ValidateEvidence(SourceEvidence evidence, string revision)
    {
        if (evidence is null || evidence.Revision != revision)
            throw new InvalidDataException("Evidence must match the exact graph revision.");
        if (!Enum.IsDefined(evidence.Kind))
            throw new InvalidDataException("Invalid provenance kind.");
        if (evidence.Line < 0 || evidence.Column < 0)
            throw new InvalidDataException("Invalid source location.");
        if (evidence.RelativeFile is null)
            throw new InvalidDataException("Source location must be explicit, including an empty root.");
    }

    private string GetPath(string project, string revision)
    {
        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("Project and revision are required.");
        var key = Encoding.UTF8.GetBytes(project + "\0" + revision);
        string name = Convert.ToHexStringLower(SHA256.HashData(key)) + ".graph.json";
        return Path.Combine(_directory, name);
    }

    private static void EnsureSameBytes(string path, byte[] expected)
    {
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
            throw new InvalidOperationException(
                "Revision already exists with different graph content. A snapshot is immutable.");
    }
}

/// <summary>
/// Additions/removals describe identity differences. ChangedNodes/ChangedEdges
/// report retained identities whose structural fields or source locations differ.
/// A method-body edit preserving those fields is outside this projection.
/// </summary>
public sealed record GraphDifference(
    string FromRevision,
    string ToRevision,
    IReadOnlyList<string> AddedNodes,
    IReadOnlyList<string> RemovedNodes,
    IReadOnlyList<string> AddedEdges,
    IReadOnlyList<string> RemovedEdges,
    IReadOnlyList<string> ChangedNodes,
    IReadOnlyList<string> ChangedEdges);
