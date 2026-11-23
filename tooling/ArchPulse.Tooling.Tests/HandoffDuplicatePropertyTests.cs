// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;
using Xunit;

namespace ArchPulse.Tooling.Tests;

public sealed class HandoffDuplicatePropertyTests
{
    private static readonly string Root = RepositoryRoot.Find(AppContext.BaseDirectory);

    [Fact]
    public void RejectsDuplicateTopLevelStatusEvenIfLastValueLooksValid()
    {
        using var fixture = new Fixture();
        var original = File.ReadAllText(fixture.RecordPath);
        File.WriteAllText(fixture.RecordPath, original.Insert(original.IndexOf('{') + 1,
            "\"status\":\"verified\","));

        Assert.Contains(HandoffAllChecker.Validate(fixture.RootPath), error =>
            error.Contains("duplicate JSON object key", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDuplicateNestedEffectStateEvenIfLastValueLooksValid()
    {
        using var fixture = new Fixture();
        var checkpoint = JsonNode.Parse(File.ReadAllText(fixture.RecordPath))!.AsObject();
        checkpoint["externalEffects"] = new JsonArray(new JsonObject
        {
            ["action"] = "inspect",
            ["idempotencyKey"] = "unique-test-key",
            ["state"] = "unknown",
            ["receipt"] = null
        });
        checkpoint["nextSafeAction"] = "reconcile remote outcome before retry";
        var json = checkpoint.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        const string original = "\"state\": \"unknown\"";
        Assert.Contains(original, json, StringComparison.Ordinal);
        File.WriteAllText(fixture.RecordPath, json.Replace(original,
            "\"state\":\"succeeded\",\"state\":\"unknown\"", StringComparison.Ordinal));

        Assert.Contains(HandoffAllChecker.Validate(fixture.RootPath), error =>
            error.Contains("duplicate JSON object key", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDuplicateSchemaPropertyRatherThanApplyingLastOne()
    {
        using var fixture = new Fixture();
        var schema = File.ReadAllText(fixture.SchemaPath);
        File.WriteAllText(fixture.SchemaPath, schema.Insert(schema.IndexOf('{') + 1,
            "\"type\":\"array\","));

        Assert.Contains(HandoffAllChecker.Validate(fixture.RootPath), error =>
            error.Contains("schema contains duplicate JSON object keys", StringComparison.Ordinal));
    }

    [Fact]
    public void PreservesExistingValidPublicCheckpoints() =>
        Assert.Empty(HandoffAllChecker.Validate(Root));

    private sealed class Fixture : IDisposable
    {
        public string RootPath { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-duplicate-handoff-" + Guid.NewGuid().ToString("N"));
        public string RecordPath => Path.Combine(RootPath, "docs/operations/agent-handoff/FOUNDATION-AUDIT-2026-11-10.json");
        public string SchemaPath => Path.Combine(RootPath, "docs/operations/agent-handoff/handoff.schema.json");

        public Fixture()
        {
            var directory = Path.GetDirectoryName(RecordPath)!;
            Directory.CreateDirectory(directory);
            foreach (var file in Directory.GetFiles(Path.Combine(Root, "docs/operations/agent-handoff"), "*.json"))
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            var requirements = Path.Combine(RootPath, "docs/requirements");
            Directory.CreateDirectory(requirements);
            File.Copy(Path.Combine(Root, "docs/requirements/task-registry.csv"),
                Path.Combine(requirements, "task-registry.csv"));
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
