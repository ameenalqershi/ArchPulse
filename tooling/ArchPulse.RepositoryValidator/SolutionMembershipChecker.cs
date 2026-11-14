// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace ArchPulse.RepositoryValidator;

/// <summary>
/// Checks the declared C# project membership of the root .sln without loading
/// MSBuild, running project targets, or treating a valid solution as policy approval.
/// </summary>
public static class SolutionMembershipChecker
{
    private static readonly Regex ProjectLine = new(
        "^Project\\(\\\"\\{(?<type>[0-9a-fA-F-]{36})\\}\\\"\\)\\s*=\\s*\\\"[^\\\"]+\\\",\\s*\\\"(?<path>[^\\\"]+)\\\",\\s*\\\"\\{(?<id>[0-9a-fA-F-]{36})\\}\\\"\\s*$",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly HashSet<string> CsharpTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC",
        "9A19103F-16F7-4668-BE54-9A1E7A4F7556"
    };
    private const string SolutionFolderType = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    public static IReadOnlyList<string> Validate(string root)
    {
        var errors = new List<string>();
        string fullRoot = Path.GetFullPath(root);
        string solution = Path.Combine(fullRoot, "ArchPulse.sln");
        if (!File.Exists(solution))
            return ["Missing solution membership file: ArchPulse.sln"];
        if (new FileInfo(solution).Length > 2_000_000)
            return ["Solution membership file exceeds inspection budget: ArchPulse.sln"];

        string[] lines;
        try { lines = File.ReadAllLines(solution); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ["Cannot inspect solution membership: ArchPulse.sln"];
        }

        var projectIds = new HashSet<Guid>();
        var projectPaths = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.StartsWith("Project(", StringComparison.Ordinal)) continue;
            Match m = ProjectLine.Match(line);
            if (!m.Success || !Guid.TryParse(m.Groups["type"].Value, out Guid type)
                || !Guid.TryParse(m.Groups["id"].Value, out Guid id))
            {
                errors.Add($"Malformed solution project entry at ArchPulse.sln:{i + 1}.");
                continue;
            }
            if (!projectIds.Add(id))
                errors.Add($"Duplicate solution project GUID at ArchPulse.sln:{i + 1}.");

            string projectType = type.ToString("D");
            if (projectType.Equals(SolutionFolderType, StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = m.Groups["path"].Value.Replace('\\', '/');
            bool isCsharp = CsharpTypes.Contains(projectType) ||
                relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
            if (!isCsharp) continue;
            if (!relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                Path.IsPathRooted(relative) || relative.Contains(':') ||
                relative.Contains("$(", StringComparison.Ordinal) ||
                relative.Contains("%(", StringComparison.Ordinal) ||
                relative.Contains('*') || relative.Contains('?'))
            {
                errors.Add($"Unsupported C# solution project path at ArchPulse.sln:{i + 1}.");
                continue;
            }

            string target;
            try { target = Path.GetFullPath(Path.Combine(fullRoot, relative)); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add($"Invalid solution project path at ArchPulse.sln:{i + 1}.");
                continue;
            }

            if (!target.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                errors.Add($"Solution project escapes repository root at ArchPulse.sln:{i + 1}.");
                continue;
            }
            string display = Path.GetRelativePath(fullRoot, target).Replace('\\', '/');
            if (!projectPaths.Add(target))
                errors.Add($"Duplicate solution project path: {display}.");
            if (!File.Exists(target))
                errors.Add($"Missing solution C# project: {display}.");
            else if (ContainsSymlink(fullRoot, target))
                errors.Add($"Symlinked solution project is not allowed: {display}.");
        }
        return errors;
    }

    private static bool ContainsSymlink(string root, string target)
    {
        string current = root;
        foreach (string component in Path.GetRelativePath(root, target).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, component);
            if (!File.Exists(current) && !Directory.Exists(current)) return false;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }
}
