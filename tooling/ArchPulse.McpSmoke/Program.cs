// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: ArchPulse.McpSmoke <loopback-mcp-url> <project> <revision>");
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
    "archpulse_linked_tests", "archpulse_feature_scope"
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
