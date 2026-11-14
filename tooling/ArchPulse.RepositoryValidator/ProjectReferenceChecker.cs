// SPDX-License-Identifier: Apache-2.0
using System.Xml;
using System.Xml.Linq;

namespace ArchPulse.RepositoryValidator;

/// <summary>
/// Checks literal project-to-project references without evaluating untrusted MSBuild.
/// This enforces only syntactic integrity and acyclicity, not approved architecture policy.
/// </summary>
public static class ProjectReferenceChecker
{
    private static readonly HashSet<string> IgnoredDirectories =
        new(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "node_modules", ".next" };

    public static IReadOnlyList<string> Validate(string root)
    {
        var errors = new List<string>();
        string absoluteRoot = Path.GetFullPath(root);
        if (!Directory.Exists(absoluteRoot))
            return ["Project reference root does not exist."];

        var discovered = ListProjects(absoluteRoot).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        // Never dereference project-file symlinks: they could lead outside the checkout.
        var paths = new List<string>();
        foreach (string path in discovered)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                errors.Add($"Symlinked project file is not allowed: {Path.GetRelativePath(absoluteRoot, path).Replace('\\', '/')}.");
            else
                paths.Add(path);
        }
        var graph = paths.ToDictionary(x => x, _ => new List<string>(), StringComparer.Ordinal);
        foreach (string path in paths)
        {
            XDocument document;
            try
            {
                using var reader = XmlReader.Create(path, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 2_000_000
                });
                document = XDocument.Load(reader);
            }
            catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
            {
                errors.Add($"Cannot inspect project references in {Display(path)}: invalid or inaccessible project XML.");
                continue;
            }

            foreach (var element in document.Descendants().Where(x => x.Name.LocalName == "ProjectReference"))
            {
                string? include = element.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(include) || include.Contains("$(", StringComparison.Ordinal)
                    || include.Contains("%(", StringComparison.Ordinal) || include.Contains("*", StringComparison.Ordinal)
                    || include.Contains("?", StringComparison.Ordinal))
                {
                    errors.Add($"Unresolved project reference in {Display(path)}; MSBuild expressions are not evaluated.");
                    continue;
                }

                string candidate;
                try
                {
                    // ProjectReference uses MSBuild paths, including Windows separators.
                    candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!,
                        include.Replace('\\', Path.DirectorySeparatorChar)));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    errors.Add($"Invalid project reference path in {Display(path)}.");
                    continue;
                }

                if (!WithinRoot(candidate))
                {
                    errors.Add($"Project reference escapes repository root: {Display(path)}.");
                    continue;
                }
                if (!graph.ContainsKey(candidate))
                {
                    errors.Add($"Missing local project reference: {Display(path)} -> {Display(candidate)}.");
                    continue;
                }
                graph[path].Add(candidate);
            }
        }

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in paths)
            Visit(project);
        return errors;

        bool WithinRoot(string path) => path.StartsWith(absoluteRoot + Path.DirectorySeparatorChar,
            StringComparison.Ordinal) || path == absoluteRoot;

        string Display(string path) => Path.GetRelativePath(absoluteRoot, path).Replace('\\', '/');

        void Visit(string project)
        {
            if (state.GetValueOrDefault(project) == 2) return;
            if (state.GetValueOrDefault(project) == 1) return;
            state[project] = 1;
            stack.Add(project);
            foreach (var target in graph[project].OrderBy(x => x, StringComparer.Ordinal))
            {
                if (state.GetValueOrDefault(target) == 1)
                {
                    int start = stack.IndexOf(target);
                    string chain = string.Join(" -> ", stack.Skip(start).Append(target).Select(Display));
                    if (emitted.Add(chain))
                        errors.Add($"Project reference cycle: {chain}.");
                }
                else if (state.GetValueOrDefault(target) == 0)
                    Visit(target);
            }
            stack.RemoveAt(stack.Count - 1);
            state[project] = 2;
        }
    }

    private static IEnumerable<string> ListProjects(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string file in Directory.EnumerateFiles(directory, "*.csproj"))
                yield return Path.GetFullPath(file);
            foreach (string child in Directory.EnumerateDirectories(directory))
                if (!IgnoredDirectories.Contains(Path.GetFileName(child)) &&
                    (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                    pending.Push(child);
        }
    }
}
