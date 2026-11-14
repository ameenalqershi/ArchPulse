// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArchPulse.Core;

// T055/S11 — experimental LOCAL claim/renew/release events; no remote authority.
public sealed record PortableTaskLeaseGrant(
    string TaskId, string AgentId, long Epoch, string SecretToken, DateTimeOffset ExpiresUtc);

public sealed record PortableTaskLeaseEvent
{
    public required string Schema { get; init; }
    public required string TaskId { get; init; }
    public required int Sequence { get; init; }
    public required long Epoch { get; init; }
    public required string Operation { get; init; }
    public required string AgentId { get; init; }
    public required string TokenSha256 { get; init; }
    public required string EnvelopeSha256 { get; init; }
    public required DateTimeOffset ObservedUtc { get; init; }
    public required DateTimeOffset ExpiresUtc { get; init; }
    public required string PreviousDigest { get; init; }
}

public sealed record PortableTaskLeaseEntry
{
    public required PortableTaskLeaseEvent Event { get; init; }
    public required string Digest { get; init; }
}

/// <summary>
/// A process-shared single-host lease experiment. A fence check is only a read-only
/// observation and is NOT a remote-write authorization or durable distributed lock.
/// A protected writer must atomically check the fence at its own mutation boundary.
/// </summary>
public sealed partial class PortableTaskLeaseStore
{
    private const string Schema = "archpulse.task-lease-event/v1";
    private const int MaximumEvents = 10000;
    private const int MaximumEventBytes = 8192;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };
    private static readonly string Genesis = new('0', 64);
    private readonly string _trustedRoot;
    private readonly TimeProvider _clock;

    [GeneratedRegex("^T[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex AgentPattern();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexPattern();
    [GeneratedRegex("^[0-9]{8}[.]json$", RegexOptions.CultureInvariant)]
    private static partial Regex FilePattern();

    public PortableTaskLeaseStore(string trustedRoot, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedRoot);
        _trustedRoot = Path.GetFullPath(trustedRoot);
        _clock = clock ?? TimeProvider.System;
    }

    public PortableTaskLeaseGrant Claim(string taskId, string agentId, TimeSpan ttl)
    {
        ValidateTask(taskId);
        ValidateAgent(agentId);
        ValidateTtl(ttl);
        using var exclusive = Lock(taskId);
        var envelope = CheckedEnvelope(taskId);
        var history = ReadEvents(taskId);
        var previous = history.Count == 0 ? null : history[^1].Event;
        var now = _clock.GetUtcNow();
        CheckClock(previous, now);
        if (previous is not null && previous.Operation != "Release" && now < previous.ExpiresUtc)
            throw new InvalidOperationException("another local agent lease is still active");
        if (previous is not null && previous.Epoch == long.MaxValue)
            Fail("lease epoch exhausted");
        var epoch = previous is null ? 1L : previous.Epoch + 1;
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var expiration = now.Add(ttl);
        var entry = WriteEvent(taskId, history, new PortableTaskLeaseEvent
        {
            Schema = Schema,
            TaskId = taskId,
            Sequence = history.Count + 1,
            Epoch = epoch,
            Operation = "Claim",
            AgentId = agentId,
            TokenSha256 = HashSecret(secret),
            EnvelopeSha256 = envelope,
            ObservedUtc = now,
            ExpiresUtc = expiration,
            PreviousDigest = history.Count == 0 ? Genesis : history[^1].Digest
        });
        return new PortableTaskLeaseGrant(taskId, agentId, entry.Event.Epoch, secret,
            entry.Event.ExpiresUtc);
    }

    public PortableTaskLeaseGrant Renew(PortableTaskLeaseGrant grant, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateTask(grant.TaskId);
        ValidateTtl(ttl);
        using var exclusive = Lock(grant.TaskId);
        var envelope = CheckedEnvelope(grant.TaskId);
        var history = ReadEvents(grant.TaskId);
        var last = history.Count == 0 ? null : history[^1].Event;
        var now = _clock.GetUtcNow();
        ValidateHolder(last, grant, envelope, now);
        var until = now.Add(ttl);
        if (until <= last!.ExpiresUtc)
            throw new InvalidOperationException("renewal must extend the existing lease");
        var entry = WriteEvent(grant.TaskId, history, last with
        {
            Sequence = history.Count + 1,
            Operation = "Renew",
            ObservedUtc = now,
            ExpiresUtc = until,
            PreviousDigest = history[^1].Digest
        });
        return grant with { ExpiresUtc = entry.Event.ExpiresUtc };
    }

    public void Release(PortableTaskLeaseGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateTask(grant.TaskId);
        using var exclusive = Lock(grant.TaskId);
        var envelope = CheckedEnvelope(grant.TaskId);
        var history = ReadEvents(grant.TaskId);
        var last = history.Count == 0 ? null : history[^1].Event;
        var now = _clock.GetUtcNow();
        ValidateHolder(last, grant, envelope, now);
        WriteEvent(grant.TaskId, history, last! with
        {
            Sequence = history.Count + 1,
            Operation = "Release",
            ObservedUtc = now,
            ExpiresUtc = now,
            PreviousDigest = history[^1].Digest
        });
    }

    /// <summary>
    /// Checks a current epoch/secret against this local ledger only. The result may
    /// become stale immediately; do NOT use it as a capability for external writes.
    /// </summary>
    public bool IsCurrent(PortableTaskLeaseGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateTask(grant.TaskId);
        using var exclusive = Lock(grant.TaskId);
        var envelope = CheckedEnvelope(grant.TaskId);
        var history = ReadEvents(grant.TaskId);
        var last = history.Count == 0 ? null : history[^1].Event;
        var now = _clock.GetUtcNow();
        CheckClock(last, now);
        return IsHolder(last, grant, envelope, now);
    }

    /// <summary>
    /// Local opt-in protected checkpoint append: check the fencing epoch and
    /// commit via T054 while holding the SAME lease lock. This fences only writes
    /// through this method; legacy direct T054 writes remain intentionally unfenced.
    /// </summary>
    public PortableTaskLedgerEntry AppendCheckpoint(
        PortableTaskLeaseGrant grant, int expectedSequence, string expectedDigest,
        string status, string[] evidenceSha256, string[] unknownEffectIds)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateTask(grant.TaskId);
        using var exclusive = Lock(grant.TaskId);
        var envelope = CheckedEnvelope(grant.TaskId);
        var history = ReadEvents(grant.TaskId);
        var current = history.Count == 0 ? null : history[^1].Event;
        ValidateHolder(current, grant, envelope, _clock.GetUtcNow());
        // Keeping the lease lock through the T054 append excludes concurrent
        // Claim/Renew/Release. T054 also validates its independent head CAS.
        return new PortableTaskLedger(_trustedRoot).Append(grant.TaskId,
            expectedSequence, expectedDigest, envelope, status,
            evidenceSha256, unknownEffectIds);
    }

    public IReadOnlyList<PortableTaskLeaseEntry> ReadEvents(string taskId)
    {
        ValidateTask(taskId);
        var directory = LeaseDirectory(taskId);
        if (!Directory.Exists(directory)) return [];
        EnsureNotLinked(directory);
        var files = Directory.EnumerateFiles(directory, "*.json")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        if (files.Length > MaximumEvents) Fail("lease history event budget exceeded");
        var result = new List<PortableTaskLeaseEntry>(files.Length);
        var previousDigest = Genesis;
        PortableTaskLeaseEvent? previous = null;
        for (var index = 0; index < files.Length; index++)
        {
            var filename = Path.GetFileName(files[index]);
            if (filename != $"{index + 1:D8}.json" || !FilePattern().IsMatch(filename))
                Fail("lease history sequence gap");
            if (new FileInfo(files[index]).LinkTarget is not null ||
                new FileInfo(files[index]).Length > MaximumEventBytes)
                Fail("unsafe lease event file");
            var bytes = File.ReadAllBytes(files[index]);
            if (bytes.Length > MaximumEventBytes) Fail("lease event file too large");
            PortableTaskLeaseEntry entry;
            try
            {
                var raw = Utf8.GetString(bytes);
                using var parsed = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 16 });
                if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                    HasRepeatedKeys(parsed.RootElement))
                    Fail("ambiguous lease event JSON");
                entry = parsed.RootElement.Deserialize<PortableTaskLeaseEntry>(Json)
                    ?? throw new InvalidDataException("empty lease record");
            }
            catch (Exception ex) when (ex is DecoderFallbackException or JsonException)
            {
                throw new InvalidDataException("invalid lease event JSON");
            }
            ValidateEvent(entry.Event, taskId, index + 1, previousDigest, previous);
            var digest = Digest(entry.Event);
            if (entry.Digest != digest) Fail("lease event digest mismatch");
            result.Add(entry);
            previousDigest = digest;
            previous = entry.Event;
        }
        return result;
    }

    private PortableTaskLeaseEntry WriteEvent(string taskId,
        IReadOnlyList<PortableTaskLeaseEntry> history, PortableTaskLeaseEvent proposed)
    {
        if (history.Count >= MaximumEvents) Fail("lease history full");
        ValidateEvent(proposed, taskId, history.Count + 1,
            history.Count == 0 ? Genesis : history[^1].Digest,
            history.Count == 0 ? null : history[^1].Event);
        var entry = new PortableTaskLeaseEntry
        {
            Event = proposed,
            Digest = Digest(proposed)
        };
        var bytes = Utf8.GetBytes(JsonSerializer.Serialize(entry, Json));
        if (bytes.Length > MaximumEventBytes) Fail("lease event exceeds size budget");
        var directory = LeaseDirectory(taskId);
        var pending = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(pending, Path.Combine(directory, $"{proposed.Sequence:D8}.json"),
                overwrite: false);
        }
        finally
        {
            if (File.Exists(pending)) File.Delete(pending);
        }
        return entry;
    }

    private FileStream Lock(string taskId)
    {
        var taskDirectory = Path.Combine(_trustedRoot, taskId);
        Directory.CreateDirectory(_trustedRoot);
        EnsureNotLinked(_trustedRoot);
        Directory.CreateDirectory(taskDirectory);
        EnsureNotLinked(taskDirectory);
        var directory = LeaseDirectory(taskId);
        Directory.CreateDirectory(directory);
        EnsureNotLinked(directory);
        var path = Path.Combine(directory, ".writer.lock");
        if (File.Exists(path) && new FileInfo(path).LinkTarget is not null)
            Fail("linked writer lock is forbidden");
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private string CheckedEnvelope(string taskId)
    {
        var snapshots = new PortableTaskLedger(_trustedRoot).Read(taskId);
        if (snapshots.Count == 0)
            throw new InvalidOperationException("task needs an existing validated checkpoint before a lease");
        return snapshots[^1].Checkpoint.EnvelopeSha256;
    }

    private string LeaseDirectory(string taskId) =>
        Path.Combine(_trustedRoot, taskId, "leases");

    private static void ValidateEvent(PortableTaskLeaseEvent? current, string taskId,
        int sequence, string previousDigest, PortableTaskLeaseEvent? previous)
    {
        if (current is null || current.Schema != Schema ||
            current.TaskId != taskId || current.Sequence != sequence ||
            current.PreviousDigest != previousDigest || current.Epoch < 1)
            Fail("lease event identity or hash chain mismatch");
        ValidateAgent(current.AgentId);
        ValidateHex(current.TokenSha256);
        ValidateHex(current.EnvelopeSha256);
        if (current.ObservedUtc.Offset != TimeSpan.Zero ||
            current.ExpiresUtc.Offset != TimeSpan.Zero ||
            current.ObservedUtc == default || current.ExpiresUtc == default)
            Fail("lease event timestamps must be UTC");
        if (current.Operation is not ("Claim" or "Renew" or "Release"))
            Fail("unknown lease event operation");
        if (previous is null)
        {
            if (current.Operation != "Claim" || current.Epoch != 1)
                Fail("first event must claim epoch one");
        }
        else
        {
            if (current.ObservedUtc < previous.ObservedUtc ||
                current.EnvelopeSha256 != previous.EnvelopeSha256)
                Fail("lease event time or source envelope reverted");
            if (current.Operation == "Claim")
            {
                if (current.Epoch != previous.Epoch + 1 ||
                    (previous.Operation != "Release" &&
                     current.ObservedUtc < previous.ExpiresUtc))
                    Fail("new claim must follow release or expiry and advance epoch");
            }
            else if (current.Epoch != previous.Epoch ||
                     current.AgentId != previous.AgentId ||
                     current.TokenSha256 != previous.TokenSha256 ||
                     previous.Operation == "Release" ||
                     current.ObservedUtc >= previous.ExpiresUtc)
            {
                Fail("renew or release requires the unexpired previous lease");
            }
        }
        if (current.Operation == "Release")
        {
            if (current.ExpiresUtc != current.ObservedUtc) Fail("released lease must expire immediately");
        }
        else if (current.ExpiresUtc <= current.ObservedUtc ||
                 current.ExpiresUtc - current.ObservedUtc > TimeSpan.FromMinutes(10))
            Fail("invalid lease expiry interval");
        if (current.Operation == "Renew" &&
            previous is not null && current.ExpiresUtc <= previous.ExpiresUtc)
            Fail("renewal failed to extend lease");
    }

    private static bool IsHolder(PortableTaskLeaseEvent? state, PortableTaskLeaseGrant grant,
        string expectedEnvelope, DateTimeOffset now)
    {
        if (state is null || state.Operation == "Release" || now >= state.ExpiresUtc ||
            state.Epoch != grant.Epoch || state.AgentId != grant.AgentId ||
            state.EnvelopeSha256 != expectedEnvelope ||
            grant.SecretToken is null || !HexPattern().IsMatch(grant.SecretToken))
            return false;
        var submitted = Convert.FromHexString(grant.SecretToken);
        var expected = Convert.FromHexString(state.TokenSha256);
        var hash = SHA256.HashData(submitted);
        return CryptographicOperations.FixedTimeEquals(hash, expected);
    }

    private static void ValidateHolder(PortableTaskLeaseEvent? state,
        PortableTaskLeaseGrant grant, string expectedEnvelope, DateTimeOffset now)
    {
        CheckClock(state, now);
        if (!IsHolder(state, grant, expectedEnvelope, now))
            throw new InvalidOperationException("lease token or fencing epoch is stale");
    }

    private static void CheckClock(PortableTaskLeaseEvent? state, DateTimeOffset now)
    {
        if (state is not null && now < state.ObservedUtc)
            Fail("local clock moved backward; refuse lease operation");
    }
    private static string HashSecret(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Convert.FromHexString(secret)));
    private static string Digest(PortableTaskLeaseEvent entry) =>
        Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(JsonSerializer.Serialize(entry, Json))));
    private static void ValidateTask(string? id)
    {
        if (id is null || !TaskPattern().IsMatch(id)) throw new ArgumentException("invalid task id");
    }
    private static void ValidateAgent(string? id)
    {
        if (id is null || !AgentPattern().IsMatch(id)) Fail("invalid agent id");
    }
    private static void ValidateHex(string? hash)
    {
        if (hash is null || !HexPattern().IsMatch(hash)) Fail("invalid digest");
    }
    private static void ValidateTtl(TimeSpan ttl)
    {
        if (ttl < TimeSpan.FromSeconds(1) || ttl > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(ttl));
    }
    private static void EnsureNotLinked(string path)
    {
        if (new DirectoryInfo(path).LinkTarget is not null) Fail("symlinked lease directory");
    }
    private static bool HasRepeatedKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in node.EnumerateObject())
                if (!seen.Add(prop.Name) || HasRepeatedKeys(prop.Value)) return true;
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray())
                if (HasRepeatedKeys(item)) return true;
        return false;
    }
    [DoesNotReturn]
    private static void Fail(string message) => throw new InvalidDataException(message);
}
