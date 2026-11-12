// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ArchPulse.Tooling;

/// <summary>Repository structural validation. This is not a production readiness gate.</summary>
public static class FoundationValidator
{
    private static readonly string[] Required =
    [
        "README.md", "README.ar.md", "LICENSE", "CONTRIBUTING.md",
        "CODE_OF_CONDUCT.md", "SECURITY.md", "GOVERNANCE.md", "SUPPORT.md",
        "MAINTAINERS.md", "ROADMAP.md", "AGENTS.md", "ArchPulse.sln",
        ".github/CODEOWNERS", ".github/dependabot.yml", ".github/workflows/ci.yml",
        "docs/requirements/specification-index.md", "docs/requirements/task-registry.csv",
        "docs/requirements/requirements.csv", "docs/operations/agent-handoff/handoff.schema.json"
    ];
    private static readonly Regex MarkdownLinks = new(@"\[[^\]]+\]\(([^)]+)\)", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(string root)
    {
        var errors = new List<string>();
        foreach (var file in Required)
            if (!File.Exists(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar))))
                errors.Add($"Missing: {file}");
        var licensePath = Path.Combine(root, "LICENSE");
        if (File.Exists(licensePath))
        {
            var text = File.ReadAllText(licensePath);
            if (!text.Contains("Apache License", StringComparison.Ordinal) ||
                !text.Contains("Version 2.0, January 2004", StringComparison.Ordinal))
                errors.Add("Root license is not recognized as standard Apache-2.0 text");
        }
        foreach (var file in SourceFiles(root, "*.cs"))
            if (!File.ReadAllText(file).Contains("SPDX-License-Identifier: Apache-2.0", StringComparison.Ordinal))
                errors.Add($"SPDX missing: {Path.GetRelativePath(root, file)}");
        foreach (var file in SourceFiles(root, "*.json"))
        {
            try { using var doc = JsonDocument.Parse(File.ReadAllText(file)); }
            catch (JsonException e) { errors.Add($"Invalid .json: {Path.GetRelativePath(root, file)}: {e.Message}"); }
        }
        foreach (var file in SourceFiles(root, "*.csproj"))
        {
            try { _ = XDocument.Load(file); }
            catch (System.Xml.XmlException e) { errors.Add($"Invalid .csproj: {Path.GetRelativePath(root, file)}: {e.Message}"); }
        }
        foreach (var file in SourceFiles(root, "*.md"))
        {
            foreach (Match match in MarkdownLinks.Matches(File.ReadAllText(file)))
            {
                var target = match.Groups[1].Value;
                if (target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith('#'))
                    continue;
                var local = target.Split('#', '?')[0];
                if (local.Length > 0 && !File.Exists(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, local))) &&
                    !Directory.Exists(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, local))))
                    errors.Add($"Broken local link: {Path.GetRelativePath(root, file)} -> {target}");
            }
        }
        var directory = Path.Combine(root, "docs", "requirements");
        foreach (var (name, count, field, headers) in new[]
        {
            ("task-registry.csv", 205, "Work ID", TraceabilityValidator.TaskHeaders),
            ("requirements.csv", 192, "Requirement ID", TraceabilityValidator.RequirementHeaders)
        })
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path)) continue;
            var rows = RegistryReader.Read(path, headers, errors);
            if (rows.Count != count || rows.Select(r => r[field]).Distinct(StringComparer.Ordinal).Count() != count)
                errors.Add($"{name}: expected {count} distinct rows, got {rows.Count}");
            if (rows.Any(r => r.TryGetValue("Implementation status", out var s) && s is not ("Planned" or "Unknown")))
                errors.Add($"{name}: do not fabricate verified statuses");
        }
        return errors;
    }

    private static IEnumerable<string> SourceFiles(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is ".git" or "bin" or "obj" or "node_modules"));
}
