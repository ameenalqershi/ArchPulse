// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Xml;
using ArchPulse.TestJourney;

if (args.Length < 2 || args[0] != "--trx")
{
    Console.Error.WriteLine("Usage: ArchPulse.TestJourney --trx <local-results.trx> [--tested-sha <40-lowercase-hex>] [--graph-sha <40-lowercase-hex>]");
    return 2;
}
var file = args[1];
string? testedSha = null, graphSha = null;
for (var i = 2; i < args.Length; i++)
{
    if (args[i] == "--tested-sha" && i + 1 < args.Length) testedSha = args[++i];
    else if (args[i] == "--graph-sha" && i + 1 < args.Length) graphSha = args[++i];
    else { Console.Error.WriteLine("Unknown or missing argument."); return 2; }
}
try
{
    var report = TrxEvidenceReader.Import(file, testedSha, graphSha);
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    { WriteIndented = true }));
    return 0;
}
catch (Exception e) when (e is IOException or XmlException or ArgumentException or InvalidDataException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"TRX import rejected ({e.GetType().Name}).");
    return 1;
}
