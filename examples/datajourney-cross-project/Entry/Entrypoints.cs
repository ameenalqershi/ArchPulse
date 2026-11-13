// SPDX-License-Identifier: Apache-2.0
using ArchPulse.DataJourney;

namespace JourneyFixture;

public static class Entrypoints
{
    public static void Named([SensitiveSource] string input) =>
        RelayService.Save(value: input, label: "classification-label");

    public static void ReturnRoundTrip([SensitiveSource] string input)
    {
        var result = RelayService.Identity(input);
        StorageAdapter.Store(result);
    }

    public static void FromSourceMethod() => StorageAdapter.Store(RelayService.Read());

    public static void Serialize([SensitiveSource] string input) => RelayService.Serialize(input);

    public static Task AsyncBoundary([SensitiveSource] string input) => RelayService.PublishAsync(input);

    public static void Conditional([SensitiveSource] string input) => RelayService.Conditional(input);

    public static void Recursive([SensitiveSource] string input) => RelayService.Recursive(input);

    public static void ClaimedSanitizer([SensitiveSource] string input) =>
        StorageAdapter.Store(RelayService.ClaimedSanitizer(input));

    public static void AssignmentInArgument([SensitiveSource] string input)
    {
        RelayService.Save(label: (input = "replacement"), value: input);
        RelayService.Save(label: "later", value: input);
    }

    public static void DynamicSql([SensitiveSource] string input) => RelayService.DynamicSql(input);

    public static void Reflection([SensitiveSource] string input) => RelayService.Reflection(input);

    public static void Overwrite([SensitiveSource] string input) => RelayService.Overwrite(input);

    public static void Unmarked(string input) => RelayService.Save("literal", input);

    public static void WrongArgument([SensitiveSource] string input) =>
        RelayService.Save(value: "public-literal", label: input);
}
