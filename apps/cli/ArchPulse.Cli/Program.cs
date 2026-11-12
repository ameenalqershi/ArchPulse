// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Roslyn;

if (args.Length != 6 || args[0] != "index" || args[2] != "--revision" || args[4] != "--project")
{
    Console.Error.WriteLine("Usage: archpulse index <path-to-C#-source> --revision <git-sha> --project <name>");
    return 2;
}

try
{
    var graph = StaticGraphIndexer.IndexDirectory(args[5], Path.GetFullPath(args[1]), args[3]);
    Console.WriteLine(JsonSerializer.Serialize(graph, new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    }));
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
