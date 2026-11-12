// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;
using Xunit;

namespace ArchPulse.Tooling.Tests;

public sealed class HandoffValidationTests
{
    private static readonly string Root = RepositoryRoot.Find(AppContext.BaseDirectory);

    [Fact]
    public void ExistingFoundationAuditMatchesSchema() =>
        Assert.Empty(HandoffChecker.Validate(Root));

    [Fact]
    public void RejectsUnknownHandoffState()
    {
        using var fixture = new HandoffFixture();
        fixture.Modify(audit => audit["status"] = "unrecognized");
        Assert.Contains(HandoffChecker.Validate(fixture.Path),
            e => e.Contains("invalid status", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInventedCommitInImmutableAudit()
    {
        using var fixture = new HandoffFixture();
        fixture.Modify(audit => audit["workingCommit"] = new string('a', 40));
        Assert.Contains(HandoffChecker.Validate(fixture.Path),
            e => e.Contains("workingCommit is not null", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMissingRequiredField()
    {
        using var fixture = new HandoffFixture();
        fixture.Modify(audit => audit.Remove("taskId"));
        Assert.Contains(HandoffChecker.Validate(fixture.Path),
            e => e.Contains("missing or unexpected required fields", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnknownSpecificationReference()
    {
        using var fixture = new HandoffFixture();
        fixture.Modify(audit => audit["specification"] = "S99");
        Assert.Contains(HandoffChecker.Validate(fixture.Path),
            e => e.Contains("specification not in index", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMalformedCommitSha()
    {
        using var fixture = new HandoffFixture();
        fixture.Modify(audit => audit["baseCommit"] = "123");
        Assert.Contains(HandoffChecker.Validate(fixture.Path),
            e => e.Contains("invalid baseCommit", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInvalidArrayStructure()
    {
        using var fixture = new HandoffFixture();
        fixture.Modify(audit => audit["completedSteps"] = "fake receipt");
        Assert.Contains(HandoffChecker.Validate(fixture.Path),
            e => e.Contains("completedSteps must be an array", StringComparison.Ordinal));
    }

    private sealed class HandoffFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-handoff-validate-" + Guid.NewGuid().ToString("N"));
        private string AuditPath => System.IO.Path.Combine(Path, "docs", "operations",
            "agent-handoff", "FOUNDATION-AUDIT-2026-11-10.json");

        public HandoffFixture()
        {
            var dst = System.IO.Path.Combine(Path, "docs", "operations", "agent-handoff");
            Directory.CreateDirectory(dst);
            foreach (var name in new[] { "handoff.schema.json", "FOUNDATION-AUDIT-2026-11-10.json" })
                File.Copy(System.IO.Path.Combine(Root, "docs", "operations", "agent-handoff", name),
                    System.IO.Path.Combine(dst, name));
            var requirements = System.IO.Path.Combine(Path, "docs", "requirements");
            Directory.CreateDirectory(requirements);
            foreach (var name in new[] { "task-registry.csv", "specification-index.md" })
                File.Copy(System.IO.Path.Combine(Root, "docs", "requirements", name),
                    System.IO.Path.Combine(requirements, name));
        }

        public void Modify(Action<JsonObject> change)
        {
            var obj = JsonNode.Parse(File.ReadAllText(AuditPath))!.AsObject();
            change(obj);
            File.WriteAllText(AuditPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
