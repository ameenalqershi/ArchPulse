// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.RegularExpressions;
using ArchPulse.Tooling;
using Xunit;

namespace ArchPulse.Tooling.Tests;

public class RegistryContractTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs/requirements/task-registry.csv")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Missing fixture root");
    }

    [Fact]
    public void HistoricalTasksAndRequirementsHaveUniqueStableIds()
    {
        var errors = new List<string>();
        var tasks = RegistryReader.Read(Path.Combine(Root, "docs/requirements/task-registry.csv"),
            TraceabilityValidator.TaskHeaders, errors);
        var requirements = RegistryReader.Read(Path.Combine(Root, "docs/requirements/requirements.csv"),
            TraceabilityValidator.RequirementHeaders, errors);
        Assert.Empty(errors);
        Assert.Equal(205, tasks.Count);
        Assert.Equal(192, requirements.Count);
        Assert.Equal(205, tasks.Select(x => x["Work ID"]).Distinct().Count());
        Assert.Equal(192, requirements.Select(x => x["Requirement ID"]).Distinct().Count());
        Assert.All(tasks, task => Assert.Matches("^T[0-9]{3}$", task["Work ID"]));
        Assert.All(requirements, requirement =>
            Assert.Matches("^R-[A-Z][A-Z0-9-]*-[0-9]{3}$", requirement["Requirement ID"]));
    }

    [Fact]
    public void HandoffReceiptSatisfiesPublishedRequiredFields()
    {
        var dir = Path.Combine(Root, "docs/operations/agent-handoff");
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "handoff.schema.json")));
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "FOUNDATION-AUDIT-2026-11-10.json")));
        var required = schema.RootElement.GetProperty("required").EnumerateArray()
            .Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        var properties = schema.RootElement.GetProperty("properties").EnumerateObject()
            .Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var actual = receipt.RootElement.EnumerateObject()
            .Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(required.Order(), actual.Order());
        Assert.Equal(required.Order(), properties.Order());
        Assert.Equal("archpulse-agent-handoff/v1",
            receipt.RootElement.GetProperty("schema").GetString());
        Assert.Matches("^T[0-9]{3}$", receipt.RootElement.GetProperty("taskId").GetString()!);
        Assert.Matches("^S[0-9]{2}$", receipt.RootElement.GetProperty("specification").GetString()!);
        Assert.True(receipt.RootElement.GetProperty("workingCommit").ValueKind == JsonValueKind.Null,
            "Historical proposal must not fabricate working commit.");
        var statuses = schema.RootElement.GetProperty("properties").GetProperty("status")
            .GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains(receipt.RootElement.GetProperty("status").GetString(), statuses);
        foreach (var key in new[] { "baseCommit", "workingCommit" })
        {
            var value = receipt.RootElement.GetProperty(key);
            if (value.ValueKind != JsonValueKind.Null)
                Assert.Matches("^[a-f0-9]{40}$", value.GetString()!);
        }
    }

    [Fact]
    public void ProposedRequirementsDoNotClaimUnverifiedImplementation()
    {
        var errors = new List<string>();
        var rows = RegistryReader.Read(Path.Combine(Root, "docs/requirements/requirements.csv"),
            TraceabilityValidator.RequirementHeaders, errors);
        Assert.Empty(errors);
        Assert.All(rows, row => Assert.Contains(row["Implementation status"], new[] { "Planned", "Unknown" }));
    }
}
