// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Roslyn;

if (args.Length < 3 || args[0] != "index")
{
    Console.Error.WriteLine("Usage: ArchPulse.Cli index <trusted.sln|trusted.csproj> <git-commit-sha> [output.json]");
    return 2;
}
try
{
    var snapshot = await new RoslynRepositoryIndexer().IndexAsync(args[1], args[2]);
    var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
    if (args.Length >= 4)
    {
        await File.WriteAllTextAsync(args[3], json);
        Console.WriteLine($"Indexed {snapshot.Nodes.Count} nodes, {snapshot.Edges.Count} edges; " +
            $"{snapshot.Diagnostics.Count} diagnostics; output: {args[3]}");
    }
    else Console.WriteLine(json);
    return snapshot.Diagnostics.Count == 0 ? 0 : 3;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    return 1;
}
