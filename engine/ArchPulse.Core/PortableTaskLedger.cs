// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArchPulse.Core;

// T054/S11 experimental single-host, offline checkpoint ledger.
// These entries store CLAIMED task state and evidence digests, not verified permissions or effects.
public sealed record PortableTaskCheckpoint
{
    public required string Schema { get; init; }
    public required string TaskId { get; init; }
    public required int Sequence { get; init; }
    public required string PreviousDigest { get; init; }
    public required string EnvelopeSha256 { get; init; }
    public required string Status { get; init; }
    public required string[] EvidenceSha256 { get; init; }
    public required string[] UnknownEffectIds { get; init; }
}

public sealed record PortableTaskLedgerEntry
{
    public required PortableTaskCheckpoint Checkpoint { get; init; }
    public required string Digest { get; init; }
}

/// <summary>
/// Local single-host append-only journal with process-shared exclusive writer lock,
/// optimistic sequence/digest preconditions, and validated, immutable numbered files.
/// A successful return does not prove directory fsync after power loss, or remote effects.
/// </summary>
public sealed partial class PortableTaskLedger
{
    public const string Schema = "archpulse.task-checkpoint/v1";
    public static readonly string GenesisDigest = new('0', 64);
    private const int MaxEntries = 10000;
    private const int MaxDocumentBytes = 65536;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    private readonly string _root;

    [GeneratedRegex("^T[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex EffectPattern();
    [GeneratedRegex("^[0-9]{8}[.]json$", RegexOptions.CultureInvariant)]
    private static partial Regex FilePattern();

    public PortableTaskLedger(string trustedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedRoot);
        _root = Path.GetFullPath(trustedRoot);
        if (Directory.Exists(_root) && new DirectoryInfo(_root).LinkTarget is not null)
            Fail("ledger root must not be a symlink");
    }

    /// <summary>Reads all committed checkpoints; rejects corrupt, missing, or forged history.</summary>
    public IReadOnlyList<PortableTaskLedgerEntry> Read(string taskId)
    {
        ValidateTask(taskId);
        var directory = TaskDirectory(taskId);
        if (!Directory.Exists(directory)) return [];
        EnsureNoLink(directory);

        var files = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => (Path: path, Name: Path.GetFileName(path)))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();
        if (files.Length > MaxEntries) Fail("task ledger exceeds event bound");

        var result = new List<PortableTaskLedgerEntry>(files.Length);
        var lastDigest = GenesisDigest;
        for (var i = 0; i < files.Length; i++)
        {
            if (!FilePattern().IsMatch(files[i].Name) ||
                files[i].Name != $"{i + 1:D8}.json")
                Fail("task ledger has invalid or missing checkpoint sequence");
            if (new FileInfo(files[i].Path).LinkTarget is not null)
                Fail("linked checkpoint is forbidden");
            if (new FileInfo(files[i].Path).Length > MaxDocumentBytes)
                Fail("task checkpoint exceeds size bound");
            var bytes = File.ReadAllBytes(files[i].Path);
            if (bytes.Length > MaxDocumentBytes) Fail("task checkpoint exceeds size bound");

            PortableTaskLedgerEntry entry;
            try
            {
                var raw = StrictUtf8.GetString(bytes);
                using var document = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 32 });
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    HasDuplicateProperties(document.RootElement))
                    Fail("ambiguous task checkpoint");
                entry = document.RootElement.Deserialize<PortableTaskLedgerEntry>(Json)
                    ?? throw new InvalidDataException("empty task checkpoint");
            }
            catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
            {
                throw new InvalidDataException("malformed task checkpoint");
            }

            Validate(entry.Checkpoint, taskId, i + 1, lastDigest);
            var digest = ComputeDigest(entry.Checkpoint);
            if (entry.Digest != digest) Fail("checkpoint content hash mismatch");
            lastDigest = digest;
            result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// Append after exact head CAS. Callers must reconcile external effects; this method never
    /// executes a command. An orphan .pending file after a crash is not a committed receipt.
    /// </summary>
    public PortableTaskLedgerEntry Append(
        string taskId, int expectedSequence, string expectedDigest,
        string envelopeSha256, string status,
        string[] evidenceSha256, string[] unknownEffectIds)
    {
        ValidateTask(taskId);
        if (expectedSequence < 0 || expectedSequence >= MaxEntries)
            throw new ArgumentOutOfRangeException(nameof(expectedSequence));
        ValidateDigest(expectedDigest);
        var directory = TaskDirectory(taskId);
        Directory.CreateDirectory(_root);
        EnsureNoLink(_root);
        Directory.CreateDirectory(directory);
        EnsureNoLink(directory);

        var lockFile = Path.Combine(directory, ".writer.lock");
        if (File.Exists(lockFile) && new FileInfo(lockFile).LinkTarget is not null)
            Fail("linked writer lock is forbidden");
        // FileShare.None is cross-process on the local filesystem. Busy => reject, not bypass.
        using var exclusive = new FileStream(lockFile, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        var history = Read(taskId);
        var currentDigest = history.Count == 0 ? GenesisDigest : history[^1].Digest;
        if (history.Count != expectedSequence || currentDigest != expectedDigest)
            throw new InvalidOperationException("stale task ledger revision");
        var checkpoint = new PortableTaskCheckpoint
        {
            Schema = Schema,
            TaskId = taskId,
            Sequence = expectedSequence + 1,
            PreviousDigest = currentDigest,
            EnvelopeSha256 = envelopeSha256,
            Status = status,
            EvidenceSha256 = evidenceSha256,
            UnknownEffectIds = unknownEffectIds
        };
        Validate(checkpoint, taskId, expectedSequence + 1, currentDigest);
        if (history.Count > 0 && history[^1].Checkpoint.EnvelopeSha256 != envelopeSha256)
            throw new InvalidOperationException("task envelope changed without migration");
        if (history.Count > 0 && history[^1].Checkpoint.UnknownEffectIds
            .Except(unknownEffectIds, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("unresolved effect cannot disappear without a verified reconciler");
        if (status == "HandoffReady" && unknownEffectIds.Length != 0)
            throw new InvalidOperationException("unknown effects must be reconciled before handoff");

        var entry = new PortableTaskLedgerEntry
        {
            Checkpoint = checkpoint,
            Digest = ComputeDigest(checkpoint)
        };
        var payload = StrictUtf8.GetBytes(JsonSerializer.Serialize(entry, Json));
        if (payload.Length > MaxDocumentBytes) Fail("task checkpoint exceeds size bound");
        var path = Path.Combine(directory, $"{checkpoint.Sequence:D8}.json");
        var pending = Path.Combine(directory, $".{Guid.NewGuid():N}.pending");
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }
            File.Move(pending, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(pending)) File.Delete(pending);
        }
        return entry;
    }

    private string TaskDirectory(string taskId) => Path.Combine(_root, taskId);

    private static string ComputeDigest(PortableTaskCheckpoint checkpoint) =>
        Convert.ToHexStringLower(SHA256.HashData(
            StrictUtf8.GetBytes(JsonSerializer.Serialize(checkpoint, Json))));

    private static void Validate(PortableTaskCheckpoint? checkpoint, string taskId,
        int expectedSequence, string expectedPrevious)
    {
        if (checkpoint is null || checkpoint.Schema != Schema || checkpoint.TaskId != taskId ||
            checkpoint.Sequence != expectedSequence || checkpoint.PreviousDigest != expectedPrevious)
            Fail("task checkpoint chain identity mismatch");
        ValidateDigest(checkpoint.EnvelopeSha256);
        ValidateDigest(checkpoint.PreviousDigest);
        if (checkpoint.Status is not ("Planned" or "Checkpointed" or "RecoveryRequired" or "HandoffReady"))
            Fail("unsupported experimental checkpoint state");
        ValidateArray(checkpoint.EvidenceSha256, "evidence", 32, ValidateDigest);
        ValidateArray(checkpoint.UnknownEffectIds, "unknown effects", 32, v =>
        {
            if (v is null || !EffectPattern().IsMatch(v)) Fail("invalid effect identifier");
        });
        if (checkpoint.Status == "HandoffReady" && checkpoint.UnknownEffectIds.Length > 0)
            Fail("unreconciled external effects at handoff");
    }

    private static void ValidateArray(string[]? values, string field, int cap, Action<string> validate)
    {
        if (values is null || values.Length > cap) Fail("invalid " + field + " count");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values!)
        {
            validate(value);
            if (!names.Add(value)) Fail("duplicate " + field);
        }
    }

    private static void ValidateTask(string? taskId)
    {
        if (taskId is null || !TaskPattern().IsMatch(taskId))
            throw new ArgumentException("taskId must be a Tnnn identifier", nameof(taskId));
    }

    private static void ValidateDigest(string? digest)
    {
        if (digest is null || !DigestPattern().IsMatch(digest))
            Fail("invalid SHA-256 digest");
    }

    private static void EnsureNoLink(string path)
    {
        if (new DirectoryInfo(path).LinkTarget is not null)
            Fail("linked task directory is forbidden");
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in element.EnumerateObject())
                if (!seen.Add(prop.Name) || HasDuplicateProperties(prop.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
                if (HasDuplicateProperties(child)) return true;
        return false;
    }

    [DoesNotReturn]
    private static void Fail(string message) => throw new InvalidDataException(message);
}
