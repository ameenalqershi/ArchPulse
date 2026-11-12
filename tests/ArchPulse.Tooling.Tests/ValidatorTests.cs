// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Tooling;
using Xunit;

namespace ArchPulse.Tooling.Tests;

public class ValidatorTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "requirements", "task-registry.csv")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Cannot locate repository fixtures");
    }

    [Fact]
    public void CurrentHistoricalBaselineIsConsistent()
    {
        Assert.Empty(TraceabilityValidator.Validate(Root()));
        Assert.Empty(FoundationValidator.Validate(Root()));
    }

    [Theory]
    [InlineData("task-registry.csv", "T002", "Work ID", "T001", "duplicate")]
    [InlineData("task-registry.csv", "T002", "Depends On IDs", "T999", "dangling dependency")]
    [InlineData("task-registry.csv", "T001", "Depends On IDs", "T002", "cycle")]
    [InlineData("task-registry.csv", "T001", "Specification", "S99", "unrecognized specification")]
    [InlineData("requirements.csv", "R-CORE-001", "Notion tasks", "T999", "dangling or missing task")]
    [InlineData("requirements.csv", "R-CORE-001", "Implementation status", "Verified", "no evidence field")]
    public void InvalidTraceabilityIsRejected(string file, string identifier, string column, string replacement, string diagnostic)
    {
        using var workspace = new FixtureWorkspace(Root());
        var path = Path.Combine(workspace.Path, "docs", "requirements", file);
        var headers = file == "task-registry.csv"
            ? TraceabilityValidator.TaskHeaders : TraceabilityValidator.RequirementHeaders;
        var errors = new List<string>();
        var rows = RegistryReader.Read(path, headers, errors);
        Assert.Empty(errors);
        var key = headers[0];
        var row = Assert.Single(rows, r => r[key] == identifier);
        row[column] = replacement;
        File.WriteAllLines(path,
            [string.Join(",", headers.Select(EscapeCsv)), ..rows.Select(r => string.Join(",", headers.Select(h => EscapeCsv(r[h]))))]);
        Assert.Contains(TraceabilityValidator.Validate(workspace.Path),
            error => error.Contains(diagnostic, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingTaskCannotPassBaseline()
    {
        using var workspace = new FixtureWorkspace(Root());
        var path = Path.Combine(workspace.Path, "docs", "requirements", "task-registry.csv");
        File.WriteAllLines(path, File.ReadAllLines(path).SkipLast(1));
        Assert.Contains(TraceabilityValidator.Validate(workspace.Path),
            error => error.Contains("expected 205", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingSpdxIsDetected()
    {
        using var workspace = new FixtureWorkspace(Root(), full: true);
        var file = Path.Combine(workspace.Path, "standalone.cs");
        File.WriteAllText(file, "namespace Example;");
        Assert.Contains(FoundationValidator.Validate(workspace.Path),
            e => e.Contains("SPDX missing: standalone.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidJsonIsDetected()
    {
        using var workspace = new FixtureWorkspace(Root(), full: true);
        File.WriteAllText(Path.Combine(workspace.Path, "invalid.json"), "{not-json}");
        Assert.Contains(FoundationValidator.Validate(workspace.Path),
            e => e.Contains("Invalid .json: invalid.json", StringComparison.Ordinal));
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    private sealed class FixtureWorkspace : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-tooling-" + Guid.NewGuid().ToString("N"));
        public FixtureWorkspace(string source, bool full = false)
        {
            Directory.CreateDirectory(Path);
            CopyFolder(System.IO.Path.Combine(source, "docs", "requirements"),
                System.IO.Path.Combine(Path, "docs", "requirements"));
            if (!full) return;
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = System.IO.Path.GetRelativePath(source, file);
                if (rel.Split(System.IO.Path.DirectorySeparatorChar).Any(s => s is ".git" or "bin" or "obj")) continue;
                var dest = System.IO.Path.Combine(Path, rel);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }
        }
        private static void CopyFolder(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source))
                File.Copy(file, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(file)));
        }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
