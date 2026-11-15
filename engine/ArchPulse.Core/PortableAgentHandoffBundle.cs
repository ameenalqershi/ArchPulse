// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArchPulse.Core;

// T057/S11: versioned offline transport only. Never treat this as approval or a capability.
public sealed record PortableAgentHandoffBundle
{
    public required string Schema { get; init; }
    public required string TaskId { get; init; }
    public required string TaskEnvelopeJson { get; init; }
    public required string TaskEnvelopeSha256 { get; init; }
    public required string ManifestSha256 { get; init; }
    public required int CheckpointSequence { get; init; }
    public required string CheckpointDigest { get; init; }
    public required string Status { get; init; }
    public required string[] UnknownEffectIds { get; init; }
    public required string[] NextSteps { get; init; }
}

/// <summary>
/// Strict bounded provider-neutral JSON for offline human-reviewed handoff.
/// Does NOT confer identity, an agent lease, permission, or external effect success.
/// </summary>
public static partial class PortableAgentHandoffJson
{
    public const string Schema = "archpulse.agent-handoff-bundle/v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
    [GeneratedRegex("^T[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex EffectPattern();
    [GeneratedRegex("(?:gh[pousr]_[A-Za-z0-9_]{16,}|github_pat_[A-Za-z0-9_]{15,}|sk-[A-Za-z0-9_-]{25,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialPattern();

    public static string Create(
        string trustedRoot, string taskId, string envelopeJson, string manifestSha256,
        IReadOnlyList<string> nextSteps)
    {
        var envelope = PortableHandoffJson.ReadTask(envelopeJson);
        if (envelope.TaskId != taskId) Fail("envelope task identity mismatch");
        var canonicalEnvelope = PortableHandoffJson.Write(envelope);
        var envelopeSha256 = Sha(Utf8.GetBytes(canonicalEnvelope));
        var checkpoints = new PortableTaskLedger(trustedRoot).Read(taskId);
        if (checkpoints.Count == 0) Fail("task has no validated checkpoints");
        var last = checkpoints[^1];
        if (last.Checkpoint.EnvelopeSha256 != envelopeSha256)
            Fail("source envelope digest does not match ledger");
        ValidateDigest(manifestSha256);
        if (!last.Checkpoint.EvidenceSha256.Contains(manifestSha256, StringComparer.Ordinal))
            Fail("manifest not anchored in latest checkpoint");
        _ = new PortableArtifactEvidenceStore(trustedRoot).ReadManifest(taskId, manifestSha256);
        ValidateSteps(nextSteps);
        if (last.Checkpoint.Status == "HandoffReady" &&
            last.Checkpoint.UnknownEffectIds.Length > 0)
            Fail("cannot hand off as ready with unknown external effects");
        var bundle = new PortableAgentHandoffBundle
        {
            Schema = Schema,
            TaskId = taskId,
            TaskEnvelopeJson = canonicalEnvelope,
            TaskEnvelopeSha256 = envelopeSha256,
            ManifestSha256 = manifestSha256,
            CheckpointSequence = last.Checkpoint.Sequence,
            CheckpointDigest = last.Digest,
            Status = last.Checkpoint.Status,
            UnknownEffectIds = last.Checkpoint.UnknownEffectIds,
            NextSteps = nextSteps.ToArray()
        };
        return Encode(bundle);
    }

    public static PortableAgentHandoffBundle Read(
        string trustedRoot, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Utf8.GetByteCount(json) > 32768 || CredentialPattern().IsMatch(json))
            Fail("unsafe or oversized bundle");
        PortableAgentHandoffBundle bundle;
        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object ||
                HasRepeatedKeys(parsed.RootElement))
                Fail("ambiguous handoff bundle");
            bundle = parsed.RootElement.Deserialize<PortableAgentHandoffBundle>(Json)
                ?? throw new InvalidDataException("empty bundle");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("malformed handoff bundle");
        }
        if (bundle.Schema != Schema || bundle.TaskId is null ||
            !TaskPattern().IsMatch(bundle.TaskId))
            Fail("unsupported handoff identity or schema");
        ValidateDigest(bundle.TaskEnvelopeSha256);
        ValidateDigest(bundle.ManifestSha256);
        ValidateDigest(bundle.CheckpointDigest);
        ValidateSteps(bundle.NextSteps);
        if (bundle.UnknownEffectIds is null || bundle.UnknownEffectIds.Length > 64 ||
            bundle.UnknownEffectIds.Any(x => x is null || !EffectPattern().IsMatch(x)) ||
            bundle.UnknownEffectIds.Distinct(StringComparer.Ordinal).Count() != bundle.UnknownEffectIds.Length)
            Fail("invalid unknown effect set");
        if (bundle.CheckpointSequence < 1 || bundle.CheckpointSequence > 10000)
            Fail("invalid checkpoint sequence");
        var source = PortableHandoffJson.ReadTask(bundle.TaskEnvelopeJson);
        if (source.TaskId != bundle.TaskId ||
            PortableHandoffJson.Write(source) != bundle.TaskEnvelopeJson ||
            Sha(Utf8.GetBytes(bundle.TaskEnvelopeJson)) != bundle.TaskEnvelopeSha256)
            Fail("envelope mismatch or noncanonical encoding");
        var checkpoints = new PortableTaskLedger(trustedRoot).Read(bundle.TaskId);
        if (checkpoints.Count == 0 || checkpoints[^1].Checkpoint.Sequence != bundle.CheckpointSequence ||
            checkpoints[^1].Digest != bundle.CheckpointDigest ||
            checkpoints[^1].Checkpoint.EnvelopeSha256 != bundle.TaskEnvelopeSha256 ||
            checkpoints[^1].Checkpoint.Status != bundle.Status ||
            !checkpoints[^1].Checkpoint.UnknownEffectIds.SequenceEqual(bundle.UnknownEffectIds) ||
            !checkpoints[^1].Checkpoint.EvidenceSha256.Contains(bundle.ManifestSha256,
                StringComparer.Ordinal))
            Fail("handoff is stale, unanchored or inconsistent");
        if (bundle.Status == "HandoffReady" && bundle.UnknownEffectIds.Length > 0)
            Fail("unresolved effects cannot be silently approved");
        _ = new PortableArtifactEvidenceStore(trustedRoot).ReadManifest(
            bundle.TaskId, bundle.ManifestSha256);
        return bundle;
    }

    private static string Encode(PortableAgentHandoffBundle bundle)
    {
        var json = JsonSerializer.Serialize(bundle, Json);
        if (Utf8.GetByteCount(json) > 32768 || CredentialPattern().IsMatch(json))
            Fail("unsafe or oversized serialized bundle");
        return json;
    }
    private static void ValidateSteps(IReadOnlyList<string>? steps)
    {
        if (steps is null || steps.Count is < 1 or > 16)
            Fail("handoff needs bounded next steps");
        foreach (var step in steps)
            if (string.IsNullOrWhiteSpace(step) || step.Length > 512 ||
                CredentialPattern().IsMatch(step))
                Fail("unsafe next step");
    }
    private static void ValidateDigest(string? value)
    {
        if (value is null || !DigestPattern().IsMatch(value))
            Fail("invalid digest");
    }
    private static bool HasRepeatedKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.EnumerateObject())
                if (!names.Add(property.Name) || HasRepeatedKeys(property.Value)) return true;
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray())
                if (HasRepeatedKeys(item)) return true;
        return false;
    }
    private static string Sha(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
    [DoesNotReturn]
    private static void Fail(string reason) => throw new InvalidDataException(reason);
}
