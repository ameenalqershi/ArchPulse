// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchPulse.Graph;
using ArchPulse.Guardian;
using ArchPulse.Roslyn;

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
};
try
{
    if (args.Length is 4 or 6 && args[0] == "index-solution"
        && args[2] == "--revision" && (args.Length == 4 || args[4] == "--store"))
    {
        var snapshot = await new SolutionGraphIndexer().IndexSolutionAsync(args[1], args[3]);
        if (args.Length == 6)
            new GraphSnapshotStore(args[5]).Save(snapshot);
        Console.WriteLine(JsonSerializer.Serialize(snapshot, jsonOptions));
        return 0;
    }

    if (args.Length is 4 or 6 && args[0] == "test-links"
        && args[2] == "--revision" && (args.Length == 4 || args[4] == "--target"))
    {
        var analysis = await new SolutionGraphIndexer().AnalyzeSolutionAsync(args[1], args[3]);
        if (args.Length == 6)
        {
            if (!analysis.Graph.Nodes.Any(n => n.SymbolId == args[5] &&
                 n.Kind == GraphNodeKind.Method))
                throw new ArgumentException("Target is not an indexed method at this revision.");
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schema = analysis.TestAssociations.Schema,
                graphRevision = analysis.Graph.Revision,
                targetSymbolId = args[5],
                mappingStatus = analysis.TestAssociations.ForTarget(args[5]).Count == 0
                    ? "Unmapped" : "MappedDirectStatic",
                executionStatus = "Unknown",
                observedCoverage = false,
                links = analysis.TestAssociations.ForTarget(args[5])
            }, jsonOptions));
        }
        else
            Console.WriteLine(JsonSerializer.Serialize(analysis.TestAssociations, jsonOptions));
        return 0;
    }

    if (args.Length == 6 && args[0] == "index"
        && args[2] == "--revision" && args[4] == "--project")
    {
        var graph = StaticGraphIndexer.IndexDirectory(args[5], Path.GetFullPath(args[1]), args[3]);
        Console.WriteLine(JsonSerializer.Serialize(graph, jsonOptions));
        return 0;
    }

    if (args.Length == 6 && args[0] == "graph-show"
        && args[2] == "--revision" && args[4] == "--store")
    {
        var snapshot = new GraphSnapshotStore(args[5]).Load(args[1], args[3])
            ?? throw new FileNotFoundException("No matching project and revision stored.");
        Console.WriteLine(JsonSerializer.Serialize(snapshot, jsonOptions));
        return 0;
    }

    if (args.Length == 8 && args[0] == "graph-diff"
        && args[2] == "--from" && args[4] == "--to" && args[6] == "--store")
    {
        var store = new GraphSnapshotStore(args[7]);
        var earlier = store.Load(args[1], args[3])
            ?? throw new FileNotFoundException("Source revision is not in the store.");
        var later = store.Load(args[1], args[5])
            ?? throw new FileNotFoundException("Target revision is not in the store.");
        Console.WriteLine(JsonSerializer.Serialize(GraphSnapshotStore.Compare(earlier, later), jsonOptions));
        return 0;
    }

    if (args.Length == 8 && args[0] == "guardian"
        && args[2] == "--revision" && args[4] == "--store" && args[6] == "--rules")
    {
        var graph = new GraphSnapshotStore(args[5]).Load(args[1], args[3])
            ?? throw new FileNotFoundException("Graph revision not found in the store.");
        var rules = JsonSerializer.Deserialize<ForbiddenDependencyRule[]>(
            File.ReadAllText(args[7]), jsonOptions)
            ?? throw new InvalidDataException("Policy document is empty.");
        var findings = ArchitectureGuardian.Evaluate(graph, rules);
        Console.WriteLine(JsonSerializer.Serialize(findings, jsonOptions));
        return findings.Any(f => f.Outcome == RuleOutcome.Violation
            && f.Severity is RuleSeverity.Error or RuleSeverity.Blocker) ? 3 : 0;
    }

    Console.Error.WriteLine("ArchPulse native .NET CLI");
    Console.Error.WriteLine("  index-solution <.sln> --revision <git-sha> [--store <directory>]");
    Console.Error.WriteLine("  test-links <trusted.sln> --revision <sha> [--target <exact-symbol-id>]");
    Console.Error.WriteLine("  index <source-dir> --revision <git-sha> --project <name>");
    Console.Error.WriteLine("  graph-show <project> --revision <git-sha> --store <directory>");
    Console.Error.WriteLine("  graph-diff <project> --from <sha> --to <sha> --store <directory>");
    Console.Error.WriteLine("  guardian <project> --revision <sha> --store <directory> --rules <json>");
    return 2;
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                           or InvalidOperationException or JsonException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
