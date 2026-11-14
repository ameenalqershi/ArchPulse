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
        // A persisted document can become invalid independently of the HTTP
        // client (manual edits, partial disk corruption, incompatible data).
        // Tag only *content* failures; permissions and I/O failures must retain
        // their distinct error semantics and must not be mislabeled HTTP 422.
        try
        {
            using var stream = File.OpenRead(path);
            var snapshot = JsonSerializer.Deserialize<GraphSnapshot>(stream, JsonOptions)
                ?? throw new InvalidDataException("Graph snapshot is empty.");
            Validate(snapshot);
            if (snapshot.Project != project || snapshot.Revision != revision)
                throw new InvalidDataException("Stored snapshot has a mismatched identity or revision.");
            return snapshot;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            throw new CorruptGraphSnapshotException(ex);
        }
    }

    public static GraphDifference Compare(GraphSnapshot previous, GraphSnapshot current)
    {
        Validate(previous);
        Validate(current);
        if (!string.Equals(previous.Project, current.Project, StringComparison.Ordinal))
            throw new InvalidOperationException("Cannot compare graph snapshots from different projects.");
        var oldNodes = previous.Nodes.Select(x => x.SymbolId).ToHashSet(StringComparer.Ordinal);
        var newNodes = current.Nodes.Select(x => x.SymbolId).ToHashSet(StringComparer.Ordinal);
        var oldEdges = previous.Edges.Select(x => x.EdgeId).ToHashSet(StringComparer.Ordinal);
        var newEdges = current.Edges.Select(x => x.EdgeId).ToHashSet(StringComparer.Ordinal);
        return new GraphDifference(previous.Revision, current.Revision,
            newNodes.Except(oldNodes).Order(StringComparer.Ordinal).ToArray(),
            oldNodes.Except(newNodes).Order(StringComparer.Ordinal).ToArray(),
            newEdges.Except(oldEdges).Order(StringComparer.Ordinal).ToArray(),
            oldEdges.Except(newEdges).Order(StringComparer.Ordinal).ToArray());
    }

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

public sealed record GraphDifference(
    string FromRevision,
    string ToRevision,
    IReadOnlyList<string> AddedNodes,
    IReadOnlyList<string> RemovedNodes,
    IReadOnlyList<string> AddedEdges,
    IReadOnlyList<string> RemovedEdges);

/// <summary>
/// A persisted graph document is unreadable or invalid, not a missing revision.
/// The inner exception is retained for trusted diagnostics; HTTP must not expose it.
/// File I/O/permission failures are deliberately excluded.
/// </summary>
public sealed class CorruptGraphSnapshotException(Exception innerException)
    : Exception("Stored graph snapshot failed content validation.", innerException);
