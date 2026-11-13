// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.DataJourney;

namespace JourneyFixture;

public static class RelayService
{
    public static void Save(string label, string value) => StorageAdapter.Store(value);

    public static string Identity(string value) => value;

    [SensitiveSource]
    public static string Read() => "FIXTURE-SECRET-DO-NOT-SERIALIZE";

    public static void Serialize(string value)
    {
        var payload = JsonSerializer.Serialize(value);
        StorageAdapter.Store(payload);
    }

    public static async Task PublishAsync(string value)
    {
        await Task.FromResult(value);
    }

    public static void Conditional(string value)
    {
        if (value.Length > 0) StorageAdapter.Store(value);
    }

    public static void Recursive(string value) => Recursive(value);

    [DeclaredSanitizer]
    public static string ClaimedSanitizer(string value) => value;

    public static void Overwrite(string value)
    {
        value = "FIXTURE-PUBLIC-DO-NOT-SERIALIZE";
        StorageAdapter.Store(value);
    }
}
