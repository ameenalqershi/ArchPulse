// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArchPulse.Core;

// S11/T056: offline, text-only, bounded, unauthenticated evidence metadata.
// A verified hash proves content consistency, not provenance, policy approval or a test result.
public sealed record PortableArtifactReference
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required string SourceCommit { get; init; }
    public required string Sha256 { get; init; }
    public required int ByteLength { get; init; }
}

public sealed record PortableArtifactManifest
{
    public required string Schema { get; init; }
    public required string TaskId { get; init; }
    public required string EnvelopeSha256 { get; init; }
    public required string BaseCommit { get; init; }
    public required string HeadCommit { get; init; }
    public required PortableArtifactReference[] Artifacts { get; init; }
}

public sealed record PortableArtifactManifestReceipt
{
    public required string Sha256 { get; init; }
    public required PortableArtifactManifest Manifest { get; init; }
}

/// <summary>
/// Local single-host, content-addressed redacted text artifacts and immutable manifests.
/// Do not expose to untrusted agents; storage root, its ancestors and writers must be trusted.
/// </summary>
public sealed partial class PortableArtifactEvidenceStore
{
    public const string Schema = "archpulse.artifact-manifest/v1";
    public const int MaximumArtifactBytes = 131072;
    private const int MaximumManifestBytes = 16384;
    private const int MaximumReferences = 16;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 24
    };
    private readonly string _root;

    [GeneratedRegex("^T[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();
    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();
    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
    [GeneratedRegex("(?:gh[pousr]_[A-Za-z0-9_]{16,}|github_pat_[A-Za-z0-9_]{15,}|sk-[A-Za-z0-9_-]{25,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)", RegexOptions.CultureInvariant)]
    private static partial Regex CredentialPattern();

    public PortableArtifactEvidenceStore(string trustedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedRoot);
        _root = Path.GetFullPath(trustedRoot);
        if (Directory.Exists(_root)) AssertNotLinked(_root);
    }

    /// <summary>Writes an immutable content-addressed artifact; caller must supply pre-redacted UTF-8 text.</summary>
    public PortableArtifactReference PutArtifact(
        string taskId, string name, string kind, string sourceCommit, string redactedText)
    {
        ValidateTask(taskId);
        ValidateName(name);
        ValidateKind(kind);
        ValidateCommit(sourceCommit);
        if (redactedText is null || redactedText.Length > MaximumArtifactBytes)
            Fail("artifact exceeds bounded character budget");
        ValidateRedactedText(redactedText);
        var content = StrictUtf8.GetBytes(redactedText);
        if (content.Length > MaximumArtifactBytes) Fail("artifact exceeds bounded byte budget");
        var envelopeSha = CheckedEnvelope(taskId);
        _ = envelopeSha; // Only an already-validated task checkpoint may own evidence.
        var sha = Sha(content);
        var directory = EnsureDirectory(taskId, "cas");
        CommitFile(directory, sha + ".txt", content);
        return new PortableArtifactReference
        {
            Name = name,
            Kind = kind,
            SourceCommit = sourceCommit,
            Sha256 = sha,
            ByteLength = content.Length
        };
    }

    /// <summary>
    /// Captures a portable manifest of existing immutable artifacts. The returned
    /// SHA-256 can be added as evidenceSha256 in a T054 checkpoint; no proof is implied.
    /// </summary>
    public PortableArtifactManifestReceipt PutManifest(
        string taskId, string baseCommit, string headCommit,
        IReadOnlyList<PortableArtifactReference> artifacts)
    {
        ValidateTask(taskId);
        ValidateCommit(baseCommit);
        ValidateCommit(headCommit);
        ArgumentNullException.ThrowIfNull(artifacts);
        var manifest = new PortableArtifactManifest
        {
            Schema = Schema,
            TaskId = taskId,
            EnvelopeSha256 = CheckedEnvelope(taskId),
            BaseCommit = baseCommit,
            HeadCommit = headCommit,
            Artifacts = artifacts.ToArray()
        };
        ValidateManifest(manifest, taskId);
        foreach (var artifact in manifest.Artifacts)
            _ = GetArtifact(taskId, artifact);
        var bytes = StrictUtf8.GetBytes(JsonSerializer.Serialize(manifest, Json));
        if (bytes.Length > MaximumManifestBytes) Fail("manifest exceeds byte budget");
        var sha = Sha(bytes);
        CommitFile(EnsureDirectory(taskId, "manifests"), sha + ".json", bytes);
        return new PortableArtifactManifestReceipt { Sha256 = sha, Manifest = manifest };
    }

    /// <summary>Read and revalidate the entire manifest and all referenced blobs.</summary>
    public PortableArtifactManifestReceipt ReadManifest(string taskId, string digest)
    {
        ValidateTask(taskId);
        ValidateHash(digest);
        var path = Path.Combine(ExistingDirectory(taskId, "manifests"), digest + ".json");
        var bytes = CheckedRead(path, MaximumManifestBytes);
        if (Sha(bytes) != digest) Fail("manifest digest mismatch");

        PortableArtifactManifest manifest;
        try
        {
            using var document = JsonDocument.Parse(StrictUtf8.GetString(bytes),
                new JsonDocumentOptions { MaxDepth = 24 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                HasRepeatedKeys(document.RootElement))
                Fail("ambiguous manifest JSON");
            manifest = document.RootElement.Deserialize<PortableArtifactManifest>(Json)
                ?? throw new InvalidDataException("empty artifact manifest");
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException("invalid artifact manifest JSON");
        }
        ValidateManifest(manifest, taskId);
        if (manifest.EnvelopeSha256 != CheckedEnvelope(taskId))
            Fail("manifest does not match current task envelope");
        foreach (var item in manifest.Artifacts)
            _ = GetArtifact(taskId, item);
        return new PortableArtifactManifestReceipt { Sha256 = digest, Manifest = manifest };
    }

    public string GetArtifact(string taskId, PortableArtifactReference reference)
    {
        ValidateTask(taskId);
        ValidateReference(reference);
        var path = Path.Combine(ExistingDirectory(taskId, "cas"), reference.Sha256 + ".txt");
        var bytes = CheckedRead(path, MaximumArtifactBytes);
        if (bytes.Length != reference.ByteLength || Sha(bytes) != reference.Sha256)
            Fail("artifact content or declared size mismatch");
        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException) { throw new InvalidDataException("non-UTF8 artifact"); }
        ValidateRedactedText(text);
        return text;
    }

    private static void ValidateManifest(PortableArtifactManifest? manifest, string taskId)
    {
        if (manifest is null || manifest.Schema != Schema || manifest.TaskId != taskId)
            Fail("invalid artifact-manifest schema or task");
        ValidateHash(manifest.EnvelopeSha256);
        ValidateCommit(manifest.BaseCommit);
        ValidateCommit(manifest.HeadCommit);
        if (manifest.Artifacts is null || manifest.Artifacts.Length == 0 ||
            manifest.Artifacts.Length > MaximumReferences)
            Fail("invalid artifact count");
        var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in manifest.Artifacts)
        {
            ValidateReference(artifact);
            if (!uniqueNames.Add(artifact.Name)) Fail("repeated artifact name");
            if (artifact.SourceCommit != manifest.BaseCommit &&
                artifact.SourceCommit != manifest.HeadCommit)
                Fail("artifact revision not represented by manifest");
        }
    }

    private static void ValidateReference(PortableArtifactReference? reference)
    {
        if (reference is null) Fail("null artifact reference");
        ValidateName(reference.Name);
        ValidateKind(reference.Kind);
        ValidateCommit(reference.SourceCommit);
        ValidateHash(reference.Sha256);
        if (reference.ByteLength <= 0 || reference.ByteLength > MaximumArtifactBytes)
            Fail("invalid artifact byte length");
    }

    private static void ValidateRedactedText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) Fail("empty artifact");
        if (CredentialPattern().IsMatch(value) || value.Contains('\0'))
            Fail("possible credential or unsafe control in artifact");
    }

    private static void ValidateTask(string? value)
    {
        if (value is null || !TaskPattern().IsMatch(value))
            throw new ArgumentException("invalid task id", nameof(value));
    }
    private static void ValidateName(string? value)
    {
        if (value is null || !NamePattern().IsMatch(value)) Fail("invalid artifact name");
    }
    private static void ValidateKind(string? value)
    {
        if (value is not ("git-diff" or "test-report" or "build-log" or
            "policy-receipt" or "decision-record"))
            Fail("unsupported artifact kind");
    }
    private static void ValidateCommit(string? value)
    {
        if (value is null || !CommitPattern().IsMatch(value))
            Fail("invalid Git commit reference");
    }
    private static void ValidateHash(string? value)
    {
        if (value is null || !HashPattern().IsMatch(value))
            Fail("invalid SHA-256");
    }

    private string CheckedEnvelope(string taskId)
    {
        var checkpoints = new PortableTaskLedger(_root).Read(taskId);
        if (checkpoints.Count == 0) Fail("artifact needs validated task ledger");
        return checkpoints[^1].Checkpoint.EnvelopeSha256;
    }
    private string EnsureDirectory(string taskId, string subsection)
    {
        Directory.CreateDirectory(_root);
        AssertNotLinked(_root);
        var task = Path.Combine(_root, taskId);
        Directory.CreateDirectory(task);
        AssertNotLinked(task);
        var dir = Path.Combine(task, subsection);
        Directory.CreateDirectory(dir);
        AssertNotLinked(dir);
        return dir;
    }
    private string ExistingDirectory(string taskId, string subsection)
    {
        var task = Path.Combine(_root, taskId);
        var dir = Path.Combine(task, subsection);
        if (!Directory.Exists(_root) || !Directory.Exists(task) || !Directory.Exists(dir))
            Fail("missing artifact repository");
        AssertNotLinked(_root);
        AssertNotLinked(task);
        AssertNotLinked(dir);
        return dir;
    }
    private static byte[] CheckedRead(string path, int maximum)
    {
        if (!File.Exists(path) || new FileInfo(path).LinkTarget is not null)
            Fail("missing or symlinked immutable artifact");
        if (new FileInfo(path).Length > maximum) Fail("artifact too large");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > maximum) Fail("artifact too large");
        return bytes;
    }
    private static void CommitFile(string directory, string name, byte[] content)
    {
        var target = Path.Combine(directory, name);
        if (File.Exists(target))
        {
            var existing = CheckedRead(target, content.Length);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing),
                    SHA256.HashData(content)) || !existing.AsSpan().SequenceEqual(content))
                Fail("conflicting immutable artifact content");
            return;
        }
        var pending = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".pending");
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            try { File.Move(pending, target, overwrite: false); }
            catch (IOException) when (File.Exists(target))
            {
                var existing = CheckedRead(target, content.Length);
                if (!existing.AsSpan().SequenceEqual(content))
                    Fail("conflicting concurrent immutable artifact");
            }
        }
        finally
        {
            if (File.Exists(pending)) File.Delete(pending);
        }
    }

    private static string Sha(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool HasRepeatedKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in element.EnumerateObject())
                if (!names.Add(prop.Name) || HasRepeatedKeys(prop.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray())
                if (HasRepeatedKeys(value)) return true;
        return false;
    }
    private static void AssertNotLinked(string path)
    {
        if (new DirectoryInfo(path).LinkTarget is not null)
            Fail("linked artifact store directory");
    }
    [DoesNotReturn]
    private static void Fail(string message) => throw new InvalidDataException(message);
}
