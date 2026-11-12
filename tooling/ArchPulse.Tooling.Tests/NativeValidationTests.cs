// SPDX-License-Identifier: Apache-2.0
using Xunit;
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;

namespace ArchPulse.Tooling.Tests;

public sealed class NativeValidationTests
{
    private static readonly string Root = RepositoryRoot.Find(AppContext.BaseDirectory);

    [Fact]
    public void BaselineTraceabilityIsConsistent() =>
        Assert.Empty(TraceabilityChecker.Validate(Root));

    [Fact]
    public void BaselineRepositoryHasRequiredFoundationContracts() =>
        Assert.Empty(RepositoryChecker.Validate(Root));

    [Fact]
    public void RejectsMissingTaskRow()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("task-registry.csv", text => string.Join("\n", text.TrimEnd((char)13, (char)10).Split('\n').SkipLast(1)));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("expected 205", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDuplicateTaskIdentifier()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("task-registry.csv", text => text.Replace("\"T002\",", "\"T001\",", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RejectsDanglingTaskDependency()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("task-registry.csv", text => text.Replace("\"T002\"", "\"T002\"", StringComparison.Ordinal)
            .Replace("\"T001\",\"S01\"", "\"T999\",\"S01\"", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("dangling dependency", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsCircularTaskDependency()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("task-registry.csv", text => text.Replace("\"—\",\"S00\"", "\"T002\",\"S00\"", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RejectsUnrecognizedSpecification()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("task-registry.csv", text => text.Replace("\"T001\",\"S01\"", "\"T001\",\"S99\"", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("unrecognized specification", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnknownRequirementTask()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("requirements.csv", text => text.Replace("\"T001,T111\"", "\"T999\"", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("dangling or missing task", StringComparison.Ordinal));
    }

    [Fact]
    public void DoesNotAcceptVerifiedWithoutEvidenceReceipt()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("requirements.csv", text => text.Replace("\"Full capability acceptance suite\",\"Planned\"",
            "\"Full capability acceptance suite\",\"Verified\"", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("no evidence field", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMalformedCsvHeader()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("task-registry.csv", text => text.Replace("\"Work ID\"", "\"Broken ID\"", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("CSV header", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMissingCanonicalSpecLink()
    {
        using var fixture = new RegistryFixture();
        fixture.Modify("specification-index.md", text => text.Replace(
            "https://app.notion.com/p/4143830ba34d81e9b337d04a89706acc",
            "https://example.invalid/spec", StringComparison.Ordinal));
        Assert.Contains(TraceabilityChecker.Validate(fixture.Path),
            e => e.Contains("missing canonical Notion link", StringComparison.Ordinal));
    }

    private sealed class RegistryFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-native-validator-" + Guid.NewGuid().ToString("N"));

        public RegistryFixture()
        {
            var target = System.IO.Path.Combine(Path, "docs", "requirements");
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(System.IO.Path.Combine(Root, "docs", "requirements")))
                File.Copy(file, System.IO.Path.Combine(target, System.IO.Path.GetFileName(file)));
        }

        public void Modify(string file, Func<string, string> change)
        {
            var path = System.IO.Path.Combine(Path, "docs", "requirements", file);
            File.WriteAllText(path, change(File.ReadAllText(path)));
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
