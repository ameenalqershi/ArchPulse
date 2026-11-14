// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchPulse.Graph;

/// <summary>
/// Experimental local-volume, append-only projection journal. Each finalized event is
/// durable via a flushed temporary file and atomic same-directory rename. It is not
/// a distributed event store or a power-loss-safe database.
/// </summary>
public sealed class GraphRevisionJournal
{
    private const int SchemaVersion = 1;
    private const string CheckpointSchema = "archpulse.graph-journal-checkpoint/v1";
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public GraphRevisionJournal(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Journal directory is required.", nameof(root));
        _root = Path.GetFullPath(root);
    }

    /// <summary>Return the reconstructed exact graph revision, or null if no events exist.</summary>
    public GraphSnapshot? Replay(string project) => ReadEvents(project).Snapshot;

    /// <summary>
    /// Create an immutable checkpoint from a fully validated journal head.
    /// A checkpoint never deletes/replaces journal events. A signed or
    /// externally trusted checkpoint is NOT provided by this local store.
    /// </summary>
    public GraphCheckpointReceipt CreateCheckpoint(string project)
    {
        string directory = ProjectPath(project);
        if (!Directory.Exists(directory))
            throw new FileNotFoundException("Graph journal has no committed revisions.");

        using var writerLock = new FileStream(Path.Combine(directory, ".writer.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var state = ReadEvents(project);
        if (state.Snapshot is null || state.Digest is null)
            throw new FileNotFoundException("Graph journal has no committed revisions.");

        byte[] snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(state.Snapshot, JsonOptions);
        string snapshotDigest = Convert.ToHexStringLower(SHA256.HashData(snapshotBytes));
        var checkpoint = new GraphJournalCheckpoint(CheckpointSchema, project,
            state.Snapshot.Revision, state.Sequence, state.Digest,
            snapshotDigest, state.Snapshot);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint, JsonOptions);
        string path = CheckpointPath(directory, state.Sequence);
        if (File.Exists(path))
        {
            VerifyExistingCheckpoint(path, bytes);
            return new GraphCheckpointReceipt(project, state.Snapshot.Revision,
                state.Sequence, state.Digest, snapshotDigest, path);
        }
        string temp = Path.Combine(directory, $".archpulse-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            try
            {
                File.Move(temp, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                VerifyExistingCheckpoint(path, bytes);
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return new GraphCheckpointReceipt(project, state.Snapshot.Revision,
            state.Sequence, state.Digest, snapshotDigest, path);
    }

    /// <summary>
    /// Trust a local immutable, content-checked checkpoint and replay only its
    /// trailing journal events. Does NOT audit events before the checkpoint;
    /// use Replay(project) for whole-history verification.
    /// </summary>
    public GraphSnapshot? ReplayFromLatestCheckpoint(string project)
    {
        string directory = ProjectPath(project);
        if (!Directory.Exists(directory)) return null;
        string? latest = Directory.GetFiles(directory, "*.checkpoint.json")
            .Order(StringComparer.Ordinal).LastOrDefault();
        if (latest is null) return Replay(project);
        var checkpoint = LoadCheckpoint(project, latest);
        return ReadEvents(project, checkpoint: checkpoint).Snapshot;
    }

    /// <summary>
    /// Reconstruct an earlier revision from the validated event prefix. Events AFTER
    /// the requested revision are intentionally not read, allowing recovery of a
    /// valid older state if a newer event is corrupt. Use Replay() to audit the head.
    /// Unknown revisions are null; the journal is never modified by a read.
    /// </summary>
    public GraphSnapshot? ReplayAt(string project, string revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("Revision is required.", nameof(revision));
        return ReadEvents(project, revision).Snapshot;
    }

    /// <summary>
    /// Append an immutable revision if expectedRevision matches the head. A retry of the
    /// exact same committed revision is idempotent; a conflicting revision fails closed.
    /// The writer lock is cross-process on a local filesystem (FileShare.None).
    /// </summary>
    public GraphJournalReceipt Append(GraphSnapshot snapshot, string? expectedRevision)
    {
        GraphSnapshotStore.Validate(snapshot);
        snapshot = Canonical(snapshot);
        string directory = ProjectPath(snapshot.Project);
        Directory.CreateDirectory(directory);
        string lockPath = Path.Combine(directory, ".writer.lock");
        using var writerLock = new FileStream(lockPath, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        var state = ReadEvents(snapshot.Project);
        if (state.Snapshot is not null &&
            string.Equals(state.Snapshot.Revision, snapshot.Revision, StringComparison.Ordinal))
        {
            if (!SameSnapshot(state.Snapshot, snapshot))
                throw new InvalidOperationException("Journal revision already exists with different contents.");
            return new GraphJournalReceipt(snapshot.Project, snapshot.Revision,
                state.Sequence, state.Digest!, state.LastPath!);
        }
        if (!string.Equals(state.Snapshot?.Revision, expectedRevision, StringComparison.Ordinal))
            throw new InvalidOperationException("Graph journal optimistic concurrency conflict.");

        var diff = state.Snapshot is null ? null : GraphSnapshotStore.Compare(state.Snapshot, snapshot);
        var changedNodeIds = diff is null
            ? snapshot.Nodes.Select(node => node.SymbolId).ToHashSet(StringComparer.Ordinal)
            : diff.AddedNodes.Concat(diff.ChangedNodes).ToHashSet(StringComparer.Ordinal);
        var changedEdgeIds = diff is null
            ? snapshot.Edges.Select(edge => edge.EdgeId).ToHashSet(StringComparer.Ordinal)
            : diff.AddedEdges.Concat(diff.ChangedEdges).ToHashSet(StringComparer.Ordinal);

        var payload = new GraphJournalEvent(
            SchemaVersion, state.Sequence + 1, snapshot.Project, snapshot.Revision,
            state.Snapshot?.Revision, state.Digest,
            snapshot.Nodes.Where(node => changedNodeIds.Contains(node.SymbolId)).ToArray(),
            diff?.RemovedNodes.ToArray() ?? [],
            snapshot.Edges.Where(edge => changedEdgeIds.Contains(edge.EdgeId)).ToArray(),
            diff?.RemovedEdges.ToArray() ?? [],
            snapshot.Diagnostics.ToArray());
        string digest = Digest(payload);
        var envelope = new GraphJournalEnvelope(payload, digest);
        string path = EventPath(directory, payload.Sequence);
        string temp = Path.Combine(directory, $".archpulse-{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            // Never overwrite a finalized event: a stale writer must not alter history.
            File.Move(temp, path, overwrite: false);
            return new GraphJournalReceipt(snapshot.Project, snapshot.Revision,
                payload.Sequence, digest, path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private (GraphSnapshot? Snapshot, long Sequence, string? Digest, string? LastPath)
        ReadEvents(string project, string? stopAtRevision = null,
            GraphJournalCheckpoint? checkpoint = null)
    {
        if (string.IsNullOrWhiteSpace(project))
            throw new ArgumentException("Project is required.", nameof(project));
        string directory = ProjectPath(project);
        if (!Directory.Exists(directory)) return (null, 0, null, null);
        var paths = Directory.GetFiles(directory, "*.event.json")
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        GraphSnapshot? current = checkpoint?.Snapshot;
        string? digest = checkpoint?.EventDigest;
        long sequence = checkpoint?.Sequence ?? 0;
        string? lastPath = checkpoint is null ? null : EventPath(directory, checkpoint.Sequence);
        long ordinal = 0;
        foreach (string path in paths)
        {
            ordinal++;
            if (!string.Equals(path, EventPath(directory, ordinal), StringComparison.Ordinal))
                throw new InvalidDataException("Graph journal sequence has a gap or unexpected filename.");
            // Check filenames through the prefix, but skip historical payload I/O
            // after accepting the independently checked local checkpoint.
            if (checkpoint is not null && ordinal <= checkpoint.Sequence) continue;
            sequence = ordinal;
            GraphJournalEnvelope envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<GraphJournalEnvelope>(
                    File.ReadAllBytes(path), JsonOptions)
                    ?? throw new InvalidDataException("Empty graph journal event.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Malformed graph journal event.", ex);
            }
            var evt = envelope.Event ??
                throw new InvalidDataException("Missing graph journal event payload.");
            if (evt.Schema != SchemaVersion || evt.Sequence != sequence ||
                !string.Equals(evt.Project, project, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(evt.Revision) ||
                !string.Equals(evt.ParentDigest, digest, StringComparison.Ordinal) ||
                !string.Equals(evt.PreviousRevision, current?.Revision, StringComparison.Ordinal) ||
                !string.Equals(envelope.Digest, Digest(evt), StringComparison.Ordinal))
                throw new InvalidDataException("Graph journal identity, chain or digest is invalid.");
            if (evt.UpsertNodes is null || evt.RemoveNodeIds is null ||
                evt.UpsertEdges is null || evt.RemoveEdgeIds is null || evt.Diagnostics is null)
                throw new InvalidDataException("Graph journal event collections are required.");
            if (current is not null && current.Revision == evt.Revision)
                throw new InvalidDataException("Graph journal revision must be unique.");
            current = Apply(current, evt);
            GraphSnapshotStore.Validate(current);
            digest = envelope.Digest;
            lastPath = path;
            if (stopAtRevision is not null &&
                string.Equals(current.Revision, stopAtRevision, StringComparison.Ordinal))
                return (current, sequence, digest, lastPath);
        }
        if (checkpoint is not null && ordinal < checkpoint.Sequence)
            throw new InvalidDataException("Checkpoint refers to missing committed journal events.");
        // No matching revision in the validated prefix: never substitute the head.
        return (stopAtRevision is null ? current : null, sequence, digest, lastPath);
    }

    private static GraphSnapshot Apply(GraphSnapshot? previous, GraphJournalEvent evt)
    {
        var nodes = previous?.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal)
            ?? new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var edges = previous?.Edges.ToDictionary(e => e.EdgeId, StringComparer.Ordinal)
            ?? new Dictionary<string, GraphEdge>(StringComparer.Ordinal);
        // Unchanged structure still receives source evidence for this exact revision.
        foreach (string id in nodes.Keys.ToArray())
        {
            var node = nodes[id];
            nodes[id] = node with { Evidence = node.Evidence with { Revision = evt.Revision } };
        }
        foreach (string id in edges.Keys.ToArray())
        {
            var edge = edges[id];
            edges[id] = edge with { Evidence = edge.Evidence with { Revision = evt.Revision } };
        }
        if (evt.RemoveNodeIds.Distinct(StringComparer.Ordinal).Count() != evt.RemoveNodeIds.Count ||
            evt.RemoveEdgeIds.Distinct(StringComparer.Ordinal).Count() != evt.RemoveEdgeIds.Count ||
            evt.UpsertNodes.Select(n => n.SymbolId).Distinct(StringComparer.Ordinal).Count() != evt.UpsertNodes.Count ||
            evt.UpsertEdges.Select(e => e.EdgeId).Distinct(StringComparer.Ordinal).Count() != evt.UpsertEdges.Count)
            throw new InvalidDataException("Graph journal event contains duplicate modifications.");
        foreach (string id in evt.RemoveNodeIds)
            if (!nodes.Remove(id)) throw new InvalidDataException("Graph journal removes a missing node.");
        foreach (string id in evt.RemoveEdgeIds)
            if (!edges.Remove(id)) throw new InvalidDataException("Graph journal removes a missing edge.");
        foreach (var node in evt.UpsertNodes) nodes[node.SymbolId] = node;
        foreach (var edge in evt.UpsertEdges) edges[edge.EdgeId] = edge;
        return new GraphSnapshot(evt.Project, evt.Revision,
            nodes.Values.OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray(),
            edges.Values.OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray(),
            evt.Diagnostics.ToArray());
    }

    private GraphJournalCheckpoint LoadCheckpoint(string project, string path)
    {
        GraphJournalCheckpoint checkpoint;
        try
        {
            checkpoint = JsonSerializer.Deserialize<GraphJournalCheckpoint>(
                File.ReadAllBytes(path), JsonOptions)
                ?? throw new InvalidDataException("Empty graph journal checkpoint.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed graph journal checkpoint.", ex);
        }

        string directory = ProjectPath(project);
        if (checkpoint.Schema != CheckpointSchema ||
            checkpoint.Sequence <= 0 ||
            !string.Equals(path, CheckpointPath(directory, checkpoint.Sequence), StringComparison.Ordinal) ||
            !string.Equals(project, checkpoint.Project, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(checkpoint.Revision) ||
            string.IsNullOrWhiteSpace(checkpoint.EventDigest) ||
            checkpoint.Snapshot is null ||
            checkpoint.Snapshot.Project != project ||
            checkpoint.Snapshot.Revision != checkpoint.Revision)
            throw new InvalidDataException("Graph checkpoint has an invalid identity or schema.");

        string actualSnapshotDigest = Convert.ToHexStringLower(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(checkpoint.Snapshot, JsonOptions)));
        if (!string.Equals(checkpoint.SnapshotDigest, actualSnapshotDigest,
                StringComparison.Ordinal))
            throw new InvalidDataException("Graph checkpoint snapshot digest mismatch.");
        GraphSnapshotStore.Validate(checkpoint.Snapshot);

        // Anchor the checkpoint to the exact finalized event at its sequence.
        // Only this anchor and the tail are read, not the historical payloads.
        string anchorPath = EventPath(directory, checkpoint.Sequence);
        GraphJournalEnvelope anchor;
        try
        {
            anchor = JsonSerializer.Deserialize<GraphJournalEnvelope>(
                File.ReadAllBytes(anchorPath), JsonOptions)
                ?? throw new InvalidDataException("Empty graph checkpoint anchor event.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Malformed graph checkpoint anchor event.", ex);
        }
        var evt = anchor.Event ??
            throw new InvalidDataException("Graph checkpoint anchor event has no payload.");
        if (evt.Schema != SchemaVersion || evt.Sequence != checkpoint.Sequence ||
            evt.Project != project || evt.Revision != checkpoint.Revision ||
            anchor.Digest != checkpoint.EventDigest ||
            !string.Equals(Digest(evt), checkpoint.EventDigest, StringComparison.Ordinal))
            throw new InvalidDataException("Graph checkpoint does not match its finalized event.");
        return checkpoint;
    }

    private static void VerifyExistingCheckpoint(string path, byte[] expected)
    {
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
            throw new InvalidDataException(
                "Committed graph checkpoint is immutable and its bytes differ.");
    }

    private static string CheckpointPath(string directory, long sequence) =>
        Path.Combine(directory, sequence.ToString("D16",
            System.Globalization.CultureInfo.InvariantCulture) + ".checkpoint.json");

    private string ProjectPath(string project)
    {
        if (string.IsNullOrWhiteSpace(project))
            throw new ArgumentException("Project is required.", nameof(project));
        return Path.Combine(_root, Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(project))));
    }

    private static GraphSnapshot Canonical(GraphSnapshot snapshot) => snapshot with
    {
        Nodes = snapshot.Nodes.OrderBy(n => n.SymbolId, StringComparer.Ordinal).ToArray(),
        Edges = snapshot.Edges.OrderBy(e => e.EdgeId, StringComparer.Ordinal).ToArray()
    };

    private static bool SameSnapshot(GraphSnapshot a, GraphSnapshot b) =>
        JsonSerializer.Serialize(Canonical(a), JsonOptions) ==
        JsonSerializer.Serialize(Canonical(b), JsonOptions);

    private static string Digest(GraphJournalEvent evt) => Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(evt, JsonOptions)));

    private static string EventPath(string directory, long sequence) =>
        Path.Combine(directory, sequence.ToString("D16", System.Globalization.CultureInfo.InvariantCulture) +
            ".event.json");

    private sealed record GraphJournalCheckpoint(
        string? Schema, string? Project, string? Revision, long Sequence,
        string? EventDigest, string? SnapshotDigest, GraphSnapshot? Snapshot);

    private sealed record GraphJournalEnvelope(GraphJournalEvent? Event, string? Digest);

    private sealed record GraphJournalEvent(
        int Schema, long Sequence, string Project, string Revision,
        string? PreviousRevision, string? ParentDigest,
        IReadOnlyList<GraphNode> UpsertNodes,
        IReadOnlyList<string> RemoveNodeIds,
        IReadOnlyList<GraphEdge> UpsertEdges,
        IReadOnlyList<string> RemoveEdgeIds,
        IReadOnlyList<string> Diagnostics);
}

public sealed record GraphJournalReceipt(string Project, string Revision,
    long Sequence, string Digest, string EventPath);

public sealed record GraphCheckpointReceipt(string Project, string Revision,
    long Sequence, string EventDigest, string SnapshotDigest, string CheckpointPath);
