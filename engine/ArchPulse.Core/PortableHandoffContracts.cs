// SPDX-License-Identifier: Apache-2.0
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArchPulse.Core;

// S11/T053: offline data contracts, not authenticated assertions or an agent executor.
public sealed record PortableTaskEnvelope
{
    public required string Schema { get; init; }
    public required string TaskId { get; init; }
    public required string Goal { get; init; }
    public required string Repository { get; init; }
    public required string BaseCommit { get; init; }
    public required string WorkingBranch { get; init; }
    public required string WorkspaceAlias { get; init; }
    public required string[] RequirementIds { get; init; }
    public required string[] AcceptanceCriteria { get; init; }
    public required string[] RequiredCapabilities { get; init; }
    public required string[] AllowedTools { get; init; }
}

public sealed record PortableCapabilityManifest
{
    public required string Schema { get; init; }
    public required string AgentId { get; init; }
    public required string[] Capabilities { get; init; }
}

public sealed record PortableApprovalReceipt
{
    public required string Schema { get; init; }
    public required string ApprovalId { get; init; }
    public required string TaskId { get; init; }
    public required string Commit { get; init; }
    public required string Decision { get; init; }
    public string? AssertedBy { get; init; }
    public string? EvidenceReference { get; init; }
}

public sealed record PortableExternalEffectReceipt
{
    public required string Schema { get; init; }
    public required string TaskId { get; init; }
    public required string Action { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string State { get; init; }
    public string? ReceiptSha256 { get; init; }
    public string? ReconciliationStep { get; init; }
}

/// <summary>Strict, bounded, non-authoritative agent-neutral JSON preflight.</summary>
public static partial class PortableHandoffJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitPattern();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
    [GeneratedRegex("^T[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
    [GeneratedRegex("^[a-z][a-z0-9._-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex CapabilityPattern();
    [GeneratedRegex("^https://github[.]com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepoPattern();
    [GeneratedRegex("(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{15,}|sk-[A-Za-z0-9_-]{25,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    public static PortableTaskEnvelope ReadTask(string json) => Parse(json, (PortableTaskEnvelope x) =>
    {
        Version(x.Schema, "archpulse.task-envelope/v1");
        TaskId(x.TaskId);
        Text(x.Goal, "goal", 2048);
        if (x.Repository is null || x.Repository.Length > 180 || !RepoPattern().IsMatch(x.Repository))
            Fail("invalid repository URL");
        Sha(x.BaseCommit, "baseCommit");
        if (string.IsNullOrWhiteSpace(x.WorkingBranch) || x.WorkingBranch.Length > 128 ||
            x.WorkingBranch.StartsWith('-') || x.WorkingBranch.StartsWith('/') ||
            x.WorkingBranch.Contains("..", StringComparison.Ordinal) ||
            x.WorkingBranch.Contains("//", StringComparison.Ordinal) ||
            x.WorkingBranch.Any(char.IsWhiteSpace) || x.WorkingBranch.Any(char.IsControl) ||
            x.WorkingBranch.Contains('\\'))
            Fail("unsafe workingBranch");
        if (x.WorkspaceAlias is null || !IdentifierPattern().IsMatch(x.WorkspaceAlias))
            Fail("workspaceAlias must be an opaque identifier");
        Unique(x.RequirementIds, "requirementIds", 1, 64, 80,
            v => v.StartsWith("R-", StringComparison.Ordinal) && IdentifierPattern().IsMatch(v));
        Unique(x.AcceptanceCriteria, "acceptanceCriteria", 1, 32, 1024, v => !string.IsNullOrWhiteSpace(v));
        Unique(x.RequiredCapabilities, "requiredCapabilities", 1, 32, 80, v => CapabilityPattern().IsMatch(v));
        Unique(x.AllowedTools, "allowedTools", 0, 32, 80, v => CapabilityPattern().IsMatch(v));
    });

    public static PortableCapabilityManifest ReadCapabilities(string json) => Parse(json, (PortableCapabilityManifest x) =>
    {
        Version(x.Schema, "archpulse.capability-manifest/v1");
        if (x.AgentId is null || !IdentifierPattern().IsMatch(x.AgentId)) Fail("invalid agentId");
        Unique(x.Capabilities, "capabilities", 1, 32, 80, v => CapabilityPattern().IsMatch(v));
    });

    public static PortableApprovalReceipt ReadApproval(string json) => Parse(json, (PortableApprovalReceipt x) =>
    {
        Version(x.Schema, "archpulse.approval-receipt/v1");
        TaskId(x.TaskId);
        Sha(x.Commit, "commit");
        if (x.ApprovalId is null || !IdentifierPattern().IsMatch(x.ApprovalId)) Fail("invalid approvalId");
        if (x.Decision is not ("Pending" or "Granted" or "Denied" or "Revoked")) Fail("invalid decision");
        if (x.AssertedBy is not null) Text(x.AssertedBy, "assertedBy", 128);
        if (x.EvidenceReference is not null) Text(x.EvidenceReference, "evidenceReference", 256);
        if (x.Decision == "Granted" &&
            (string.IsNullOrWhiteSpace(x.AssertedBy) || string.IsNullOrWhiteSpace(x.EvidenceReference)))
            Fail("granted claim needs assertedBy and evidenceReference; external authentication still required");
    });

    public static PortableExternalEffectReceipt ReadEffect(string json) => Parse(json, (PortableExternalEffectReceipt x) =>
    {
        Version(x.Schema, "archpulse.external-effect-receipt/v1");
        TaskId(x.TaskId);
        if (x.Action is null || !CapabilityPattern().IsMatch(x.Action)) Fail("invalid action");
        if (x.IdempotencyKey is null || !IdentifierPattern().IsMatch(x.IdempotencyKey))
            Fail("invalid idempotencyKey");
        if (x.State is not ("Planned" or "Succeeded" or "Failed" or "Unknown")) Fail("invalid effect state");
        if (x.ReceiptSha256 is not null && !DigestPattern().IsMatch(x.ReceiptSha256))
            Fail("invalid receipt hash");
        if (x.ReconciliationStep is not null) Text(x.ReconciliationStep, "reconciliationStep", 1024);
        if (x.State == "Succeeded" && x.ReceiptSha256 is null)
            Fail("succeeded claim requires a digest, not proof of remote success");
        if (x.State == "Unknown" &&
            (x.ReceiptSha256 is not null || string.IsNullOrWhiteSpace(x.ReconciliationStep)))
            Fail("unknown effect requires reconciliation step and no success receipt");
    });

    public static string Write<T>(T value) where T : notnull =>
        JsonSerializer.Serialize(value, Options);

    private static T Parse<T>(string json, Action<T> validate) where T : class
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > 65536) Fail("JSON exceeds size budget");
        if (TokenPattern().IsMatch(json)) Fail("possible credential in handoff JSON");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object || HasDuplicateKeys(doc.RootElement))
                Fail("ambiguous or non-object handoff JSON");
            var model = doc.RootElement.Deserialize<T>(Options);
            if (model is null) Fail("null handoff JSON");
            validate(model!);
            return model!;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("malformed or unsupported portable handoff JSON");
        }
    }

    private static bool HasDuplicateKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateKeys(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                if (HasDuplicateKeys(item)) return true;
        return false;
    }

    private static void Unique(string[]? values, string field, int min, int max, int length,
        Func<string, bool> valid)
    {
        if (values is null || values.Length < min || values.Length > max) Fail("invalid " + field);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values!)
            if (value is null || value.Length > length || !valid(value) || !seen.Add(value))
                Fail("invalid or duplicate " + field);
    }

    private static void Text(string? value, string field, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max) Fail("invalid " + field);
    }
    private static void TaskId(string? id)
    {
        if (id is null || !TaskPattern().IsMatch(id)) Fail("invalid taskId");
    }
    private static void Sha(string? sha, string field)
    {
        if (sha is null || !CommitPattern().IsMatch(sha)) Fail("invalid " + field);
    }
    private static void Version(string? actual, string expected)
    {
        if (actual != expected) Fail("unsupported schema version");
    }
    private static void Fail(string message) => throw new InvalidDataException(message);
}
