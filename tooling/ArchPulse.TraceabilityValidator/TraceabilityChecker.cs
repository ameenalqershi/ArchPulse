// SPDX-License-Identifier: Apache-2.0
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace ArchPulse.TraceabilityValidator;

public static class RepositoryRoot
{
    public static string Find(string start)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(start));
             directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArchPulse.sln")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("ArchPulse.sln not found in parent directories.");
    }
}

public static partial class TraceabilityChecker
{
    public const int BaselineSpecs = 34;
    public const int BaselineTasks = 205;
    public const int BaselineRequirements = 192;

    private static readonly string[] TaskHeaders =
    [
        "Work ID", "Task", "Area", "Gate", "Priority", "Status in Notion",
        "Depends On IDs", "Specification", "Requirement IDs", "Notion URL"
    ];
    private static readonly string[] RequirementHeaders =
    [
        "Requirement ID", "Expected behavior", "Notion tasks",
        "Acceptance evidence required", "Implementation status", "Source specification"
    ];
    private static readonly HashSet<string> AllowedStatuses = ["Planned", "Ready", "In Progress", "Blocked", "In Review"];

    [GeneratedRegex(@"^S\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SpecId();
    [GeneratedRegex(@"^T\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskId();
    [GeneratedRegex(@"^R-[A-Z][A-Z0-9-]*-\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex RequirementId();
    [GeneratedRegex(@"S\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex SpecRefs();
    [GeneratedRegex(@"T\d{3}", RegexOptions.CultureInvariant)]
    private static partial Regex TaskRefs();
    [GeneratedRegex(@"^\| (S\d{2}) \|", RegexOptions.CultureInvariant)]
    private static partial Regex SpecRow();

    public static IReadOnlyList<string> Validate(string repositoryRoot)
    {
        var errors = new List<string>();
        string requirementsPath = Path.Combine(repositoryRoot, "docs", "requirements");
        var specs = new List<string>();
        try
        {
            foreach (var line in File.ReadLines(Path.Combine(requirementsPath, "specification-index.md")))
            {
                var m = SpecRow().Match(line);
                if (!m.Success) continue;
                specs.Add(m.Groups[1].Value);
                if (!line.Contains("https://app.notion.com/p/", StringComparison.Ordinal))
                    errors.Add($"{m.Groups[1].Value}: missing canonical Notion link");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add($"specification-index.md: {ex.Message}");
        }

        var expectedSpecs = Enumerable.Range(0, BaselineSpecs).Select(i => $"S{i:00}").ToHashSet(StringComparer.Ordinal);
        if (specs.Count != BaselineSpecs || !specs.ToHashSet(StringComparer.Ordinal).SetEquals(expectedSpecs))
            errors.Add("specification-index.md: missing, duplicate, or out-of-range Sxx");
        if (specs.Count != specs.Distinct(StringComparer.Ordinal).Count())
            errors.Add("specification-index.md: duplicate Sxx");

        var tasks = ReadCsv(Path.Combine(requirementsPath, "task-registry.csv"), TaskHeaders, errors);
        var requirements = ReadCsv(Path.Combine(requirementsPath, "requirements.csv"), RequirementHeaders, errors);
        var taskIds = tasks.Select(r => r["Work ID"]).ToList();
        CheckIdentifiers(taskIds, Enumerable.Range(1, BaselineTasks).Select(i => $"T{i:000}").ToHashSet(),
            TaskId(), "Work ID", errors);
        var requirementIds = requirements.Select(r => r["Requirement ID"]).ToList();
        if (requirementIds.Count != BaselineRequirements)
            errors.Add($"Requirement ID: expected {BaselineRequirements} rows; got {requirementIds.Count}");
        if (requirementIds.Count != requirementIds.Distinct(StringComparer.Ordinal).Count())
            errors.Add("Requirement ID: duplicate identifiers");
        foreach (var id in requirementIds.Where(id => !RequirementId().IsMatch(id)))
            errors.Add($"Requirement ID: invalid ID '{id}'");

        var knownTasks = taskIds.ToHashSet(StringComparer.Ordinal);
        var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var row in tasks)
        {
            var id = row["Work ID"];
            if (!AllowedStatuses.Contains(row["Status in Notion"]))
                errors.Add($"{id}: unsupported or unverified task status");
            if (row["Priority"] is not ("P0" or "P1" or "P2"))
                errors.Add($"{id}: invalid priority");
            if (!expectedSpecs.Contains(row["Specification"]))
                errors.Add($"{id}: unrecognized specification {row["Specification"]}");
            if (!row["Notion URL"].StartsWith("https://app.notion.com/p/", StringComparison.Ordinal))
                errors.Add($"{id}: invalid Notion URL");
            string[] refs = TaskRefs().Matches(row["Depends On IDs"]).Select(m => m.Value).ToArray();
            if (row["Depends On IDs"] != "—" && refs.Length == 0)
                errors.Add($"{id}: unparseable dependencies");
            if (refs.Any(dep => !knownTasks.Contains(dep)))
                errors.Add($"{id}: dangling dependency");
            if (refs.Contains(id))
                errors.Add($"{id}: self dependency");
            dependencies[id] = refs;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        void Walk(string current)
        {
            if (visited.Contains(current)) return;
            if (!visiting.Add(current))
            {
                errors.Add($"Dependency cycle through {current}");
                return;
            }
            if (dependencies.TryGetValue(current, out var deps))
                foreach (var dep in deps) Walk(dep);
            visiting.Remove(current);
            visited.Add(current);
        }
        foreach (var task in dependencies.Keys) Walk(task);

        foreach (var row in requirements)
        {
            var id = row["Requirement ID"];
            if (row["Implementation status"] is not ("Planned" or "Unknown"))
                errors.Add($"{id}: implemented/verified status has no evidence field");
            var specRefs = SpecRefs().Matches(row["Source specification"]).Select(m => m.Value).ToArray();
            if (specRefs.Length == 0 || specRefs.Any(spec => !expectedSpecs.Contains(spec)))
                errors.Add($"{id}: invalid source specification");
            var refs = TaskRefs().Matches(row["Notion tasks"]).Select(m => m.Value).ToArray();
            if (refs.Length == 0 || refs.Any(task => !knownTasks.Contains(task)))
                errors.Add($"{id}: dangling or missing task reference");
        }
        return errors;
    }

    public static IReadOnlyList<Dictionary<string, string>> ReadCsv(
        string path, IReadOnlyList<string> expectedHeaders, List<string> errors)
    {
        var rows = new List<Dictionary<string, string>>();
        try
        {
            using var parser = new TextFieldParser(path, Encoding.UTF8)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");
            var headers = parser.ReadFields();
            if (headers is null || !headers.SequenceEqual(expectedHeaders))
            {
                errors.Add($"{Path.GetFileName(path)}: CSV header differs from documented schema");
                return rows;
            }
            while (!parser.EndOfData)
            {
                var line = parser.LineNumber;
                var fields = parser.ReadFields();
                if (fields is null || fields.Length != headers.Length || fields.Any(string.IsNullOrWhiteSpace))
                {
                    errors.Add($"{Path.GetFileName(path)}:{line}: missing or malformed field");
                    continue;
                }
                rows.Add(headers.Select((h, i) => (h, val: fields[i])).ToDictionary(x => x.h, x => x.val));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or MalformedLineException)
        {
            errors.Add($"{Path.GetFileName(path)}: unreadable CSV: {ex.Message}");
        }
        return rows;
    }

    private static void CheckIdentifiers(List<string> ids, HashSet<string> expected,
        Regex pattern, string field, List<string> errors)
    {
        if (ids.Count != expected.Count)
            errors.Add($"{field}: expected {expected.Count} rows; got {ids.Count}");
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            errors.Add($"{field}: duplicate identifiers");
        foreach (var id in ids.Where(id => !pattern.IsMatch(id)))
            errors.Add($"{field}: invalid ID '{id}'");
        var found = ids.ToHashSet(StringComparer.Ordinal);
        if (!found.SetEquals(expected))
        {
            var missing = expected.Except(found).Order(StringComparer.Ordinal).Take(8);
            var extra = found.Except(expected).Order(StringComparer.Ordinal).Take(8);
            if (missing.Any()) errors.Add($"{field}: missing {string.Join(",", missing)}");
            if (extra.Any()) errors.Add($"{field}: unexpected {string.Join(",", extra)}");
        }
    }
}
