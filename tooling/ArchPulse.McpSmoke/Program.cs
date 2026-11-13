// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length is not (3 or 4))
{
    Console.Error.WriteLine("Usage: ArchPulse.McpSmoke <loopback-mcp-url> <project> <revision> [feature-signals-fixture-project]");
    return 2;
}
var uri = new Uri(args[0], UriKind.Absolute);
if (uri.Host != "127.0.0.1" || uri.Scheme != Uri.UriSchemeHttp || uri.AbsolutePath != "/mcp")
{
    Console.Error.WriteLine("Smoke client refuses nonlocal endpoint or non-/mcp paths.");
    return 2;
}
await using var client = await McpClient.CreateAsync(new HttpClientTransport(new()
{
    Endpoint = uri
}));
var tools = await client.ListToolsAsync();
string[] needed = [
    "archpulse_open_graph", "archpulse_graph_snapshot", "archpulse_symbol_context",
    "archpulse_find_evidenced_path", "archpulse_guardian_check",
    "archpulse_linked_tests", "archpulse_feature_scope", "archpulse_feature_signals",
    "archpulse_feature_impact", "archpulse_feature_reviews"
];
var names = tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
foreach (var neededTool in needed)
    if (!names.Contains(neededTool))
        throw new InvalidDataException($"Missing MCP registered tool: {neededTool}");

var resources = await client.ListResourcesAsync();
if (!resources.Any(resource => resource.Uri == "ui://archpulse/graph"))
{
    // Resource templates may be used by the SDK for a static URI.
    var templates = await client.ListResourceTemplatesAsync();
    if (!templates.Any(t => t.UriTemplate == "ui://archpulse/graph"))
        throw new InvalidDataException("MCP Apps UI graph resource is not registered.");
}
var result = await client.CallToolAsync("archpulse_open_graph",
    new Dictionary<string, object?> {
        ["project"] = args[1], ["revision"] = args[2]
    });
if (result.IsError == true) throw new InvalidDataException("MCP graph tool reported an error.");
if (result.StructuredContent is not JsonElement graph)
    throw new InvalidDataException("MCP graph tool did not return structured content.");
if (graph.GetProperty("revision").GetString() != args[2] ||
    graph.GetProperty("project").GetString() != args[1])
    throw new InvalidDataException("MCP graph identity/revision did not match input.");
if (graph.GetProperty("nodes").GetArrayLength() < 2 ||
    graph.GetProperty("edges").GetArrayLength() < 1)
    throw new InvalidDataException("MCP graph was unexpectedly empty.");

var firstMethod = graph.GetProperty("nodes").EnumerateArray()
    .FirstOrDefault(n => n.GetProperty("kind").GetString() == "Method");
if (firstMethod.ValueKind != JsonValueKind.Undefined)
{
    var selection = await client.CallToolAsync("archpulse_linked_tests",
        new Dictionary<string, object?> {
            ["project"] = args[1], ["revision"] = args[2],
            ["symbolId"] = firstMethod.GetProperty("symbolId").GetString()
        });
    if (selection.IsError == true ||
        selection.StructuredContent is not JsonElement mapping ||
        mapping.GetProperty("targetSymbolId").GetString() !=
            firstMethod.GetProperty("symbolId").GetString() ||
        mapping.GetProperty("observedCoverage").GetBoolean() ||
        mapping.GetProperty("executionStatus").GetString() != "Unknown" ||
        mapping.GetProperty("mappingStatus").GetString() is not
            ("NotIndexed" or "Unmapped" or "MappedDirectStatic"))
        throw new InvalidDataException("MCP static test links incorrectly inferred runtime outcomes.");
}

// T023: a real authored feature is selected through the live MCP protocol.
var featureResult = await client.CallToolAsync("archpulse_feature_scope",
    new Dictionary<string, object?> {
        ["project"] = args[1], ["revision"] = args[2],
        ["featureId"] = "create-invoice"
    });
if (featureResult.IsError == true ||
    featureResult.StructuredContent is not JsonElement feature ||
    feature.GetProperty("schema").GetString() != "archpulse.feature-scope/v1" ||
    feature.GetProperty("nodes").GetArrayLength() != 2 ||
    feature.GetProperty("staticEdges").GetArrayLength() < 1 ||
    feature.GetProperty("executionStatus").GetString() != "Unknown" ||
    feature.GetProperty("observedCoverage").GetBoolean() ||
    feature.GetProperty("feature").GetProperty("members")[0]
        .GetProperty("claim").GetString() != "Designed")
    throw new InvalidDataException("MCP feature scope fabricated evidence or lost static membership.");
var unknownFeature = await client.CallToolAsync("archpulse_feature_scope",
    new Dictionary<string, object?> {
        ["project"] = args[1], ["revision"] = args[2], ["featureId"] = "invented"
    });
if (unknownFeature.IsError != true)
    throw new InvalidDataException("MCP tool did not reject an unknown feature.");

// T024: source signals are non-binding suggestions, never approved features.
var signalProject = args.Length == 4 ? args[3] : args[1];
var candidatesResult = await client.CallToolAsync("archpulse_feature_signals",
    new Dictionary<string, object?> {
        ["project"] = signalProject, ["revision"] = args[2]
    });
if (candidatesResult.IsError == true ||
    candidatesResult.StructuredContent is not JsonElement discovery ||
    discovery.GetProperty("runtimeStatus").GetString() != "Unknown" ||
    discovery.GetProperty("scope").GetString() !=
        "SourceSignalsOnly;HumanReviewRequired;NotFeatureMembership")
    throw new InvalidDataException("MCP source signal disclosure was missing or overclaimed.");
if (args.Length == 4)
{
    var signals = discovery.GetProperty("signals").EnumerateArray().ToArray();
    if (discovery.GetProperty("discoveryStatus").GetString() != "IndexedCandidates" ||
        signals.Length != 4 ||
        !signals.Any(x => x.GetProperty("kind").GetString() == "ControllerAction") ||
        !signals.Any(x => x.GetProperty("kind").GetString() == "NamedHandler") ||
        !signals.Any(x => x.GetProperty("kind").GetString() == "TestDeclaration") ||
        signals.Any(x => x.GetProperty("businessClaim").GetString() != "Inferred" ||
                         x.GetProperty("source").GetProperty("kind").GetString() != "Static"))
        throw new InvalidDataException("T024 MCP source signal fixture omitted or forged evidence.");
}
var invalidSignalRevision = await client.CallToolAsync("archpulse_feature_signals",
    new Dictionary<string, object?> {
        ["project"] = signalProject, ["revision"] = "not-a-stored-revision"
    });
if (invalidSignalRevision.IsError != true)
    throw new InvalidDataException("MCP feature discovery accepted an unknown revision.");

// T026: locally declared synthetic reviewer receipt, never authenticated or
// elevated to evidence of business verification. MCP exposes READ ONLY.
if (args.Length == 4)
{
    var reviewed = await client.CallToolAsync("archpulse_feature_reviews",
        new Dictionary<string, object?> { ["project"] = args[1],
            ["revision"] = args[2] });
    if (reviewed.IsError == true ||
        reviewed.StructuredContent is not JsonElement review ||
        review.GetProperty("scope").GetString() !=
            "LocalHumanDeclared;NotAuthenticated;NotBusinessVerified" ||
        review.GetProperty("sequence").GetInt32() != 1 ||
        !review.GetProperty("members").EnumerateArray().Any(member =>
            member.GetProperty("disposition").GetString() ==
                "OperatorApprovedCandidate") ||
        review.GetProperty("history")[0].GetProperty("actorKind").GetString() !=
            "LocalHumanDeclared")
        throw new InvalidDataException("MCP review receipts were missing, promoted or forged.");
}
var unknownReview = await client.CallToolAsync("archpulse_feature_reviews",
    new Dictionary<string, object?> { ["project"] = args[1],
        ["revision"] = "missing-revision" });
if (unknownReview.IsError != true)
    throw new InvalidDataException("MCP review accepted an unknown graph revision.");

// T025: an exact, read-only two-revision candidate impact query. No
// structural changes must never imply that a full Git PR is unaffected.
if (args.Length == 4)
{
    var impact = await client.CallToolAsync("archpulse_feature_impact",
        new Dictionary<string, object?> {
            ["project"] = args[1],
            ["fromRevision"] = args[2],
            ["toRevision"] = args[2] + "-future"
        });
    if (impact.IsError == true ||
        impact.StructuredContent is not JsonElement impactResult ||
        impactResult.GetProperty("schema").GetString() != "archpulse.feature-impact/v1" ||
        impactResult.GetProperty("evidenceScope").GetString() !=
            "CandidateImpact;StructureOnly;NotGitPRDiff;NotObservedRuntimeOrApproval" ||
        impactResult.GetProperty("runtimeStatus").GetString() != "Unknown" ||
        impactResult.GetProperty("impacts").GetArrayLength() != 0)
        throw new InvalidDataException("MCP structural feature-impact protocol overclaimed change evidence.");
}
var missingImpact = await client.CallToolAsync("archpulse_feature_impact",
    new Dictionary<string, object?> {
        ["project"] = args[1], ["fromRevision"] = args[2],
        ["toRevision"] = "unknown-revision"
    });
if (missingImpact.IsError != true)
    throw new InvalidDataException("MCP feature impact accepted an unknown revision.");

var resourceResult = await client.ReadResourceAsync("ui://archpulse/graph");
if (!resourceResult.Contents.Any(c => c is TextResourceContents x
    && x.Text.Contains("archpulse_graph_snapshot", StringComparison.Ordinal)))
    throw new InvalidDataException("MCP Apps HTML resource does not contain graph host bridge.");

var missing = await client.CallToolAsync("archpulse_graph_snapshot",
    new Dictionary<string, object?> {
        ["project"] = args[1], ["revision"] = "a-revision-that-is-not-stored"
    });
if (missing.IsError != true)
    throw new InvalidDataException("An unknown revision was not rejected.");
Console.WriteLine($"MCP HTTP protocol validated: {tools.Count} tools; UI resource; " +
    $"{graph.GetProperty("nodes").GetArrayLength()} real nodes, " +
    $"{graph.GetProperty("edges").GetArrayLength()} evidenced edges; missing revision rejected.");
return 0;
