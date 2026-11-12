// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchPulse.RepositoryValidator;

/// <summary>
/// Checks the historic S25 foundation audit against the declared handoff schema.
/// This is NOT a substitute for independent cross-agent takeover or full JSON Schema validation.
/// </summary>
public static partial class HandoffChecker
{
    [GeneratedRegex(@"^T[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskPattern();
    [GeneratedRegex(@"^S[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SpecPattern();
    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitPattern();

    public static IReadOnlyList<string> Validate(string root)
    {
        var errors = new List<string>();
        var dir = Path.Combine(root, "docs", "operations", "agent-handoff");
        var schemaPath = Path.Combine(dir, "handoff.schema.json");
        var auditPath = Path.Combine(dir, "FOUNDATION-AUDIT-2026-11-10.json");
        try
        {
            using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
            using var audit = JsonDocument.Parse(File.ReadAllText(auditPath));
            var schemaRoot = schema.RootElement;
            var auditRoot = audit.RootElement;

            var required = schemaRoot.GetProperty("required").EnumerateArray()
                .Select(p => p.GetString() ?? "").ToHashSet(StringComparer.Ordinal);
            var properties = schemaRoot.GetProperty("properties").EnumerateObject()
                .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var present = auditRoot.EnumerateObject().Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (!required.SetEquals(properties))
                errors.Add("handoff.schema.json: required and properties differ");
            if (!required.SetEquals(present))
                errors.Add("FOUNDATION-AUDIT: missing or unexpected required fields");

            if (!TryString(auditRoot, "schema", out var schemaVersion)
                || schemaVersion != "archpulse-agent-handoff/v1")
                errors.Add("FOUNDATION-AUDIT: incorrect schema version");

            if (!TryString(auditRoot, "taskId", out var taskId)
                || !TaskPattern().IsMatch(taskId))
                errors.Add("FOUNDATION-AUDIT: invalid taskId");
            else
            {
                var taskCsv = Path.Combine(root, "docs", "requirements", "task-registry.csv");
                if (!File.ReadLines(taskCsv).Any(line =>
                        line.StartsWith($"\"{taskId}\",", StringComparison.Ordinal)))
                    errors.Add("FOUNDATION-AUDIT: taskId not in task registry");
            }

            if (!TryString(auditRoot, "specification", out var specId)
                || !SpecPattern().IsMatch(specId))
                errors.Add("FOUNDATION-AUDIT: invalid specification");
            else
            {
                var specsIndex = Path.Combine(root, "docs", "requirements", "specification-index.md");
                if (!File.ReadLines(specsIndex).Any(line =>
                        line.StartsWith($"| {specId} |", StringComparison.Ordinal)))
                    errors.Add("FOUNDATION-AUDIT: specification not in index");
            }

            var validStates = schemaRoot.GetProperty("properties")
                .GetProperty("status").GetProperty("enum")
                .EnumerateArray().Select(v => v.GetString() ?? "")
                .ToHashSet(StringComparer.Ordinal);
            if (!TryString(auditRoot, "status", out var status)
                || !validStates.Contains(status))
                errors.Add("FOUNDATION-AUDIT: invalid status");

            foreach (var name in new[] { "baseCommit", "workingCommit" })
            {
                if (!auditRoot.TryGetProperty(name, out var value))
                    continue;
                if (value.ValueKind != JsonValueKind.Null
                    && (value.ValueKind != JsonValueKind.String
                        || !CommitPattern().IsMatch(value.GetString() ?? "")))
                    errors.Add($"FOUNDATION-AUDIT: invalid {name}");
            }

            if (!auditRoot.TryGetProperty("workingCommit", out var working)
                || working.ValueKind != JsonValueKind.Null)
                errors.Add("FOUNDATION-AUDIT: workingCommit is not null in immutable source audit");

            foreach (var name in new[]
            {
                "completedSteps", "unverifiedSteps", "changedFiles", "testResults",
                "pullRequests", "externalEffects", "knownBlockers", "risks", "requiredCapabilities"
            })
            {
                if (!auditRoot.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
                    errors.Add($"FOUNDATION-AUDIT: {name} must be an array");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException
                                   or KeyNotFoundException or UnauthorizedAccessException)
        {
            errors.Add($"FOUNDATION-AUDIT: malformed schema or audit: {ex.Message}");
        }
        return errors;
    }

    private static bool TryString(JsonElement root, string key, out string value)
    {
        value = "";
        if (!root.TryGetProperty(key, out var element)
            || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? "";
        return value.Length > 0;
    }
}
