// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using ArchPulse.TraceabilityValidator;

namespace ArchPulse.RepositoryValidator;

public static partial class RepositoryChecker
{
    private static readonly string[] RequiredFiles =
    [
        "README.md", "README.ar.md", "LICENSE", "CONTRIBUTING.md",
        "CODE_OF_CONDUCT.md", "SECURITY.md", "GOVERNANCE.md", "SUPPORT.md",
        "MAINTAINERS.md", "ROADMAP.md", "AGENTS.md", "ArchPulse.sln",
        ".github/CODEOWNERS", ".github/dependabot.yml", ".github/workflows/ci.yml",
        "docs/requirements/specification-index.md", "docs/requirements/task-registry.csv",
        "docs/requirements/requirements.csv", "docs/operations/agent-handoff/handoff.schema.json"
    ];

    [GeneratedRegex(@"\[[^\]]+\]\(([^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinks();

    public static IReadOnlyList<string> Validate(string root)
    {
        var errors = new List<string>();
        foreach (var relative in RequiredFiles)
            if (!File.Exists(Path.Combine(root, relative)))
                errors.Add($"Missing: {relative}");

        var licensePath = Path.Combine(root, "LICENSE");
        if (File.Exists(licensePath))
        {
            var license = File.ReadAllText(licensePath);
            if (!license.Contains("Apache License", StringComparison.Ordinal)
                || !license.Contains("Version 2.0, January 2004", StringComparison.Ordinal))
                errors.Add("Root license is not recognized as standard Apache-2.0 text");
        }

        foreach (var path in EnumerateSourceFiles(root))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            string extension = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                if (extension == ".cs")
                {
                    if (!File.ReadAllText(path).Contains("SPDX-License-Identifier: Apache-2.0", StringComparison.Ordinal))
                        errors.Add($"SPDX missing: {relative}");
                }
                else if (extension == ".json")
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                }
                else if (extension == ".csproj")
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                    using var reader = XmlReader.Create(path, settings);
                    while (reader.Read()) { }
                }
                else if (extension == ".md")
                {
                    foreach (Match match in MarkdownLinks().Matches(File.ReadAllText(path)))
                    {
                        string target = match.Groups[1].Value;
                        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                            || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                            || target.StartsWith('#')) continue;
                        string local = target.Split('#', '?')[0];
                        if (local.Length == 0) continue;
                        string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, Uri.UnescapeDataString(local)));
                        if (!File.Exists(resolved) && !Directory.Exists(resolved))
                            errors.Add($"Broken local link: {relative} -> {target}");
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or XmlException or ArgumentException or UriFormatException)
            {
                errors.Add($"Invalid {extension}: {relative}: {ex.Message}");
            }
        }

        string requirementPath = Path.Combine(root, "docs", "requirements");
        foreach (var (name, count, key) in new[]
        {
            ("task-registry.csv", TraceabilityChecker.BaselineTasks, "Work ID"),
            ("requirements.csv", TraceabilityChecker.BaselineRequirements, "Requirement ID")
        })
        {
            string path = Path.Combine(requirementPath, name);
            if (!File.Exists(path)) continue;
            // Structural check only; deeper schema, dependencies and cycles are the responsibility of TraceabilityChecker.
            var data = ReadRows(path);
            if (data is null) { errors.Add($"{name}: unreadable CSV"); continue; }
            if (!data.Any() || !data[0].ContainsKey(key)
                || data.Count != count || data.Select(r => r.GetValueOrDefault(key, "")).Distinct().Count() != count)
                errors.Add($"{name}: expected {count} distinct rows, got {data.Count}");
            if (data.Any(r => r.GetValueOrDefault("Implementation status", "Planned") is not ("Planned" or "Unknown")))
                errors.Add($"{name}: do not fabricate verified statuses");
        }
        return errors;
    }

    private static List<Dictionary<string, string>>? ReadRows(string path)
    {
        try
        {
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(path)
            {
                TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true
            };
            parser.SetDelimiters(",");
            string[]? headers = parser.ReadFields();
            if (headers is null) return null;
            var rows = new List<Dictionary<string, string>>();
            while (!parser.EndOfData)
            {
                string[]? fields = parser.ReadFields();
                if (fields is null || fields.Length != headers.Length) return null;
                rows.Add(headers.Select((name, i) => (name, value: fields[i]))
                    .ToDictionary(x => x.name, x => x.value));
            }
            return rows;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.VisualBasic.FileIO.MalformedLineException or ArgumentException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                if (new[] { ".git", "bin", "obj", "node_modules", ".next" }
                    .Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                    continue;
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                pending.Push(child);
            }
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                if (Path.GetExtension(file) is ".cs" or ".json" or ".csproj" or ".md")
                    yield return file;
            }
        }
    }
}
