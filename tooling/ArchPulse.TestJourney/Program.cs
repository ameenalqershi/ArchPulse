// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Xml;
using ArchPulse.TestJourney;
using ArchPulse.Graph;

if (args.Length < 2 || args[0] != "--trx")
{
    Console.Error.WriteLine("Usage: ArchPulse.TestJourney --trx <local-results.trx> [--tested-sha <40-lowercase-hex>] [--graph-sha <40-lowercase-hex>] [--store <trusted-local-store> --project <exact-project>]");
    return 2;
}
var file = args[1];
string? testedSha = null, graphSha = null, store = null, project = null;
for (var i = 2; i < args.Length; i++)
{
    if (args[i] == "--tested-sha" && testedSha is null && i + 1 < args.Length) testedSha = args[++i];
    else if (args[i] == "--graph-sha" && graphSha is null && i + 1 < args.Length) graphSha = args[++i];
    else if (args[i] == "--store" && store is null && i + 1 < args.Length) store = args[++i];
    else if (args[i] == "--project" && project is null && i + 1 < args.Length) project = args[++i];
    else { Console.Error.WriteLine("Unknown, duplicate or missing argument."); return 2; }
}
try
{
    if ((store is null) != (project is null) ||
        store is not null && graphSha is null)
    {
        Console.Error.WriteLine("Candidate correlation requires --store, --project and --graph-sha.");
        return 2;
    }
    var report = TrxEvidenceReader.Import(file, testedSha, graphSha);
    object output = report;
    if (store is not null && project is not null && graphSha is not null)
    {
        var graph = new GraphSnapshotStore(store).Load(project, graphSha)
            ?? throw new InvalidDataException("Requested graph revision is not stored.");
        var links = new StaticTestAssociationStore(store).Load(graph)
            ?? throw new InvalidDataException("No source-backed test declaration sidecar exists.");
        output = TrxSourceCandidateMatcher.Reconcile(graph, links, report);
    }
    Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    { WriteIndented = true }));
    return 0;
}
catch (Exception e) when (e is IOException or XmlException or ArgumentException or InvalidDataException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"TRX import rejected ({e.GetType().Name}).");
    return 1;
}
