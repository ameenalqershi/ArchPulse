// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace ArchPulse.Tooling;

/// <summary>Validates the versioned historical S00–S33/T001–T205/R baseline;
/// registry status is not evidence of implemented behavior.</summary>
public static class TraceabilityValidator
{
    public const int SpecBaseline = 34;
    public const int TaskBaseline = 205;
    public const int RequirementBaseline = 192;
    public static readonly string[] TaskHeaders =
    [
        "Work ID", "Task", "Area", "Gate", "Priority", "Status in Notion",
        "Depends On IDs", "Specification", "Requirement IDs", "Notion URL"
    ];
    public static readonly string[] RequirementHeaders =
    [
        "Requirement ID", "Expected behavior", "Notion tasks",
        "Acceptance evidence required", "Implementation status", "Source specification"
    ];
    private static readonly Regex SpecLine = new(@"^\| (S\d{2}) \|", RegexOptions.Compiled);
    private static readonly Regex SpecRef = new(@"S\d{2}", RegexOptions.Compiled);
    private static readonly Regex TaskRef = new(@"T\d{3}", RegexOptions.Compiled);
    private static readonly Regex TaskId = new(@"^T\d{3}$", RegexOptions.Compiled);
    private static readonly Regex RequirementId = new(@"^R-[A-Z][A-Z0-9-]*-\d{3}$", RegexOptions.Compiled);
    private static readonly HashSet<string> ExpectedSpecs =
        Enumerable.Range(0, SpecBaseline).Select(i => $"S{i:00}").ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> ExpectedTasks =
        Enumerable.Range(1, TaskBaseline).Select(i => $"T{i:000}").ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> Validate(string root)
    {
        var errors = new List<string>();
        var requirementDirectory = Path.Combine(root, "docs", "requirements");
        var index = Path.Combine(requirementDirectory, "specification-index.md");
        var specIds = new List<string>();
        try
        {
            foreach (var line in File.ReadLines(index))
            {
                var match = SpecLine.Match(line);
                if (!match.Success) continue;
                var id = match.Groups[1].Value;
                specIds.Add(id);
                if (!line.Contains("https://app.notion.com/p/", StringComparison.Ordinal))
                    errors.Add($"{id}: missing canonical Notion link");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add($"specification-index.md: {ex.Message}");
        }
        if (specIds.Count != SpecBaseline || !specIds.ToHashSet().SetEquals(ExpectedSpecs))
            errors.Add("specification-index.md: missing, duplicate, or out-of-range Sxx");
        if (specIds.Count != specIds.Distinct(StringComparer.Ordinal).Count())
            errors.Add("specification-index.md: duplicate Sxx");

        var tasks = RegistryReader.Read(Path.Combine(requirementDirectory, "task-registry.csv"), TaskHeaders, errors);
        var requirements = RegistryReader.Read(Path.Combine(requirementDirectory, "requirements.csv"), RequirementHeaders, errors);
        var taskIds = CheckIds(tasks, "Work ID", ExpectedTasks, TaskId, errors);
        CheckIds(requirements, "Requirement ID", null, RequirementId, errors, RequirementBaseline);
        var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var row in tasks)
        {
            var id = row["Work ID"];
            if (!new[] {"Planned","Ready","In Progress","Blocked","In Review"}.Contains(row["Status in Notion"]))
                errors.Add($"{id}: unsupported or unverified task status");
            if (!new[] {"P0","P1","P2"}.Contains(row["Priority"]))
                errors.Add($"{id}: invalid priority");
            if (!ExpectedSpecs.Contains(row["Specification"]))
                errors.Add($"{id}: unrecognized specification {row["Specification"]}");
            if (!row["Notion URL"].StartsWith("https://app.notion.com/p/", StringComparison.Ordinal))
                errors.Add($"{id}: invalid Notion URL");
            var refs = TaskRef.Matches(row["Depends On IDs"]).Select(x => x.Value).ToArray();
            if (row["Depends On IDs"] != "—" && refs.Length == 0)
                errors.Add($"{id}: unparseable dependencies");
            if (refs.Any(x => !taskIds.Contains(x)))
                errors.Add($"{id}: dangling dependency");
            if (refs.Contains(id))
                errors.Add($"{id}: self dependency");
            dependencies[id] = refs;
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        void Walk(string task)
        {
            if (visited.Contains(task)) return;
            if (!visiting.Add(task)) { errors.Add($"Dependency cycle through {task}"); return; }
            foreach (var dependency in dependencies.GetValueOrDefault(task) ?? [])
                Walk(dependency);
            visiting.Remove(task);
            visited.Add(task);
        }
        foreach (var task in dependencies.Keys) Walk(task);
        foreach (var row in requirements)
        {
            var id = row["Requirement ID"];
            if (row["Implementation status"] is not ("Planned" or "Unknown"))
                errors.Add($"{id}: implemented/verified status has no evidence field");
            var sources = SpecRef.Matches(row["Source specification"]).Select(m => m.Value).ToArray();
            if (sources.Length == 0 || sources.Any(x => !ExpectedSpecs.Contains(x)))
                errors.Add($"{id}: invalid source specification");
            var refs = TaskRef.Matches(row["Notion tasks"]).Select(m => m.Value).ToArray();
            if (refs.Length == 0 || refs.Any(x => !taskIds.Contains(x)))
                errors.Add($"{id}: dangling or missing task reference");
        }
        return errors;
    }

    private static HashSet<string> CheckIds(IReadOnlyList<Dictionary<string,string>> rows,
        string field, HashSet<string>? expected, Regex pattern, List<string> errors, int? expectedCount = null)
    {
        var ids = rows.Select(x => x[field]).ToList();
        var found = ids.ToHashSet(StringComparer.Ordinal);
        var size = expectedCount ?? expected?.Count ?? 0;
        if (ids.Count != size) errors.Add($"{field}: expected {size} rows; got {ids.Count}");
        if (found.Count != ids.Count) errors.Add($"{field}: duplicate identifiers");
        foreach (var id in found.Where(x => !pattern.IsMatch(x)).Order())
            errors.Add($"{field}: invalid ID '{id}'");
        if (expected != null)
        {
            var missing = expected.Except(found).Order().Take(8).ToArray();
            var extra = found.Except(expected).Order().Take(8).ToArray();
            if (missing.Length != 0) errors.Add($"{field}: missing {string.Join(",", missing)}");
            if (extra.Length != 0) errors.Add($"{field}: unexpected {string.Join(",", extra)}");
        }
        return found;
    }
}
