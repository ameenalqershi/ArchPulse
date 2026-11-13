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

    if (args.Length >= 4 && args[0] == "test-links" && args[2] == "--revision")
    {
        string? target = null, directory = null;
        if ((args.Length - 4) % 2 != 0) throw new ArgumentException("Flag requires an argument.");
        for (var i = 4; i < args.Length; i += 2)
        {
            if (args[i] == "--target" && target is null) target = args[i + 1];
            else if (args[i] == "--store" && directory is null) directory = args[i + 1];
            else throw new ArgumentException("Unexpected or repeated test-links flag.");
        }
        var analysis = await new SolutionGraphIndexer().AnalyzeSolutionAsync(args[1], args[3]);
        if (target is not null && !analysis.Graph.Nodes.Any(n =>
            n.SymbolId == target && n.Kind == GraphNodeKind.Method))
            throw new ArgumentException("Target is not an indexed method at this revision.");
        if (directory is not null)
        {
            new GraphSnapshotStore(directory).Save(analysis.Graph);
            new StaticTestAssociationStore(directory).Save(analysis.Graph, analysis.TestAssociations);
        }
        if (target is not null)
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schema = analysis.TestAssociations.Schema,
                graphRevision = analysis.Graph.Revision,
                targetSymbolId = target,
                mappingStatus = analysis.TestAssociations.ForTarget(target).Count == 0
                    ? "Unmapped" : "MappedDirectStatic",
                executionStatus = "Unknown",
                observedCoverage = false,
                links = analysis.TestAssociations.ForTarget(target)
            }, jsonOptions));
        else
            Console.WriteLine(JsonSerializer.Serialize(analysis.TestAssociations, jsonOptions));
        return 0;
    }

    // T024/S05: discover source-only candidates; never auto-approve, create
    // BusinessFeature membership, or infer that a test passed or endpoint ran.
    if (args.Length is 4 or 6 && args[0] == "feature-discover" &&
        args[2] == "--revision" && (args.Length == 4 || args[4] == "--store"))
    {
        var analysis = await new SolutionGraphIndexer().AnalyzeSolutionAsync(args[1], args[3]);
        if (args.Length == 6)
        {
            new GraphSnapshotStore(args[5]).Save(analysis.Graph);
            new FeatureDiscoveryStore(args[5]).Save(analysis.Graph, analysis.FeatureSignals);
        }
        Console.WriteLine(JsonSerializer.Serialize(analysis.FeatureSignals, jsonOptions));
        return 0;
    }

    if (args.Length == 6 && args[0] == "feature-signals" &&
        args[2] == "--revision" && args[4] == "--store")
    {
        var graph = new GraphSnapshotStore(args[5]).Load(args[1], args[3])
            ?? throw new FileNotFoundException("Exact graph revision is not stored.");
        var report = new FeatureDiscoveryStore(args[5]).Load(graph)
            ?? throw new FileNotFoundException("Feature signals NotIndexed for this revision.");
        Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
        return 0;
    }

    // T023/S05: trust the local operator to supply business meanings; source
    // symbols are checked against an existing exact graph, never guessed.
    if (args.Length == 8 && args[0] == "feature-import" &&
        args[2] == "--revision" && args[4] == "--store" && args[6] == "--manifest")
    {
        var graph = new GraphSnapshotStore(args[5]).Load(args[1], args[3])
            ?? throw new FileNotFoundException("The exact indexed graph is required.");
        var info = new FileInfo(args[7]);
        if (!info.Exists || info.Length > 2_000_000)
            throw new InvalidDataException("Feature manifest must exist and be at most 2MB.");
        var catalog = FeatureCatalogStore.Parse(File.ReadAllText(args[7]));
        new FeatureCatalogStore(args[5]).Save(graph, catalog);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schema = catalog.Schema, graph.Project, graph.Revision,
            importedFeatures = catalog.Features.Count,
            membershipScope = "ManualCandidateMembership",
            executionStatus = "Unknown"
        }, jsonOptions));
        return 0;
    }

    if (args.Length is 6 or 8 && args[0] == "feature-show" &&
        args[2] == "--revision" && args[4] == "--store" &&
        (args.Length == 6 || args[6] == "--id"))
    {
        var graph = new GraphSnapshotStore(args[5]).Load(args[1], args[3])
            ?? throw new FileNotFoundException("The exact indexed graph is required.");
        var catalog = new FeatureCatalogStore(args[5]).Load(graph)
            ?? throw new FileNotFoundException("No feature catalog for the exact graph revision.");
        object result = args.Length == 8
            ? FeatureAtlas.Project(graph, catalog, args[7])
            : catalog;
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
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

    // T025/S05: read only structural graph deltas and a human-authored
    // FROM-revision catalog. This is not a full PR/source-body impact analysis.
    if (args.Length == 8 && args[0] == "feature-impact" &&
        args[2] == "--from" && args[4] == "--to" && args[6] == "--store")
    {
        var graphs = new GraphSnapshotStore(args[7]);
        var before = graphs.Load(args[1], args[3])
            ?? throw new FileNotFoundException("Exact FROM graph revision was not indexed.");
        var after = graphs.Load(args[1], args[5])
            ?? throw new FileNotFoundException("Exact TO graph revision was not indexed.");
        var catalog = new FeatureCatalogStore(args[7]).Load(before)
            ?? throw new FileNotFoundException("FROM revision human feature catalog NotIndexed.");
        Console.WriteLine(JsonSerializer.Serialize(
            FeatureImpactAnalyzer.Assess(before, after, catalog), jsonOptions));
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
    Console.Error.WriteLine("  test-links <trusted.sln> --revision <sha> [--store <directory>] [--target <exact-symbol-id>]");
    Console.Error.WriteLine("  index <source-dir> --revision <git-sha> --project <name>");
    Console.Error.WriteLine("  feature-discover <trusted.sln> --revision <sha> [--store <directory>]");
    Console.Error.WriteLine("  feature-signals <project> --revision <sha> --store <directory>");
    Console.Error.WriteLine("  feature-import <project> --revision <sha> --store <directory> --manifest <json>");
    Console.Error.WriteLine("  feature-show <project> --revision <sha> --store <directory> [--id <feature-slug>]");
    Console.Error.WriteLine("  graph-show <project> --revision <git-sha> --store <directory>");
    Console.Error.WriteLine("  feature-impact <project> --from <sha> --to <sha> --store <directory>");
    Console.Error.WriteLine("  graph-diff <project> --from <sha> --to <sha> --store <directory>");
    Console.Error.WriteLine("  guardian <project> --revision <sha> --store <directory> --rules <json>");
    return 2;
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                           or InvalidOperationException or JsonException or KeyNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
