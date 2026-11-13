// SPDX-License-Identifier: Apache-2.0
using System.Text.Json.Serialization;
using ArchPulse.Graph;
using ArchPulse.Guardian;

var builder = WebApplication.CreateBuilder(args);

// Security boundary for this first milestone:
// Only local loopback, read-only graph; no remote client auth implemented.
builder.WebHost.UseUrls("http://127.0.0.1:5217");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 131072);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    string path = config["ARCHPULSE_GRAPH_STORE"]
        ?? Path.Combine(Path.GetTempPath(), "archpulse-graph");
    return new GraphSnapshotStore(path);
});
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    string path = config["ARCHPULSE_GRAPH_STORE"]
        ?? Path.Combine(Path.GetTempPath(), "archpulse-graph");
    return new StaticTestAssociationStore(path);
});
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    string path = config["ARCHPULSE_GRAPH_STORE"]
        ?? Path.Combine(Path.GetTempPath(), "archpulse-graph");
    return new FeatureCatalogStore(path);
});
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var path = config["ARCHPULSE_GRAPH_STORE"]
        ?? Path.Combine(Path.GetTempPath(), "archpulse-graph");
    return new FeatureDiscoveryStore(path);
});
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var path = config["ARCHPULSE_GRAPH_STORE"]
        ?? Path.Combine(Path.GetTempPath(), "archpulse-graph");
    return new FeatureReviewJournal(path);
});
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var path = config["ARCHPULSE_GRAPH_STORE"]
        ?? Path.Combine(Path.GetTempPath(), "archpulse-graph");
    return new LocalDataJourneyStore(path);
});
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    Service = "ArchPulse.GraphReadApi",
    Scope = "local-read-only-experimental",
    ProductionReady = false
}));

app.MapGet("/api/graphs/{project}/{revision}", (GraphSnapshotStore store,
    string project, string revision) =>
{
    var graph = store.Load(project, revision);
    return graph is null ? Results.NotFound() : Results.Ok(graph);
});

// T027/S06 experimental metadata-only local method DataJourney.
 // Does not run compilation from HTTP; annotations, sanitizer claims and
 // 'NoCandidateInSubset' are NOT validated privacy or runtime guarantees.
app.MapGet("/api/graphs/{project}/{revision}/datajourney-local",
    (GraphSnapshotStore graphs, LocalDataJourneyStore journey,
     string project, string revision) =>
    {
        var graph = graphs.Load(project, revision);
        if (graph is null) return Results.NotFound();
        try
        {
            var report = journey.Load(graph);
            if (report is null)
                return Results.Ok(new
                {
                    schema = LocalDataJourneyContract.Schema, project, revision,
                    journeyStatus = "NotIndexed", scope = LocalDataJourneyContract.Scope,
                    runtimeStatus = LocalDataJourneyContract.RuntimeStatus,
                    findings = Array.Empty<LocalDataFlowFinding>()
                });
            if (report.Findings.Count > 500)
                return Results.Problem("DataJourney response exceeds local read bound.",
                    statusCode: 413);
            return Results.Ok(report);
        }
        catch (InvalidDataException)
        {
            return Results.Problem("Source-derived DataJourney sidecar failed validation.",
                statusCode: 422);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem("Source-derived DataJourney sidecar is malformed.",
                statusCode: 422);
        }
    });

// T024/S05: previously indexed source-signal candidates only. No on-demand
// MSBuild evaluation, route registration, feature auto-approval or test execution.
app.MapGet("/api/graphs/{project}/{revision}/feature-signals",
    (GraphSnapshotStore graphs, FeatureDiscoveryStore signals,
     string project, string revision) =>
    {
        var graph = graphs.Load(project, revision);
        if (graph is null) return Results.NotFound();
        try
        {
            var report = signals.Load(graph);
            if (report is null)
                return Results.Ok(new
                {
                    schema = FeatureDiscoveryContract.Schema, project, revision,
                    discoveryStatus = "NotIndexed",
                    scope = FeatureDiscoveryContract.Scope,
                    runtimeStatus = "Unknown",
                    signals = Array.Empty<FeatureCandidateSignal>()
                });
            if (report.Signals.Count > 500)
                return Results.Problem("Source signals exceed local API response limit.",
                    statusCode: 413);
            return Results.Ok(new
            {
                schema = report.Schema, project, revision,
                discoveryStatus = "IndexedCandidates",
                scope = report.DiscoveryScope, runtimeStatus = report.RuntimeStatus,
                signals = report.Signals,
                diagnostics = report.Diagnostics
            });
        }
        catch (InvalidDataException)
        {
            return Results.Problem("Stored source-signal report failed validation.", statusCode: 422);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem("Stored source-signal report is unreadable.", statusCode: 422);
        }
    });

// T026/S05 local operator-declared review receipt history. No writes are
// accepted by this unauthenticated loopback HTTP API. Operator labels are not
// authenticated identities and cannot promote candidate mapping to Verified.
app.MapGet("/api/graphs/{project}/{revision}/feature-reviews",
    (GraphSnapshotStore graphs, FeatureCatalogStore features,
     FeatureReviewJournal journal, string project, string revision) =>
    {
        var graph = graphs.Load(project, revision);
        if (graph is null) return Results.NotFound();
        try
        {
            var catalog = features.Load(graph);
            if (catalog is null)
                return Results.Ok(new
                {
                    schema = FeatureReviewJournal.Schema, project, revision,
                    catalogStatus = "NotIndexed", reviewStatus = "NotIndexed",
                    scope = FeatureReviewJournal.Scope,
                    history = Array.Empty<FeatureReviewReceipt>()
                });
            var summary = journal.Read(graph, catalog);
            if (summary.History.Count > 200 || summary.Members.Count > 500)
                return Results.Problem("Review history exceeds bounded read response.",
                    statusCode: 413);
            return Results.Ok(summary);
        }
        catch (InvalidDataException)
        {
            return Results.Problem("Feature review receipt chain is invalid.", statusCode: 422);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem("Feature review receipt cannot be parsed.", statusCode: 422);
        }
    });

// T023/S05 experimental feature overlay: human-authored candidate membership,
// with exact graph node validation. No server-side imports, writes or inference.
app.MapGet("/api/graphs/{project}/{revision}/features",
    (GraphSnapshotStore graphs, FeatureCatalogStore features,
     string project, string revision) =>
    {
        var graph = graphs.Load(project, revision);
        if (graph is null) return Results.NotFound();
        try
        {
            var catalog = features.Load(graph);
            return Results.Ok(new
            {
                schema = FeatureAtlas.Schema, project, revision,
                catalogStatus = catalog is null ? "NotIndexed" : "OperatorCurated",
                membershipScope = "ManualCandidateMembership",
                executionStatus = "Unknown", observedCoverage = false,
                features = catalog?.Features ?? []
            });
        }
        catch (InvalidDataException)
        {
            return Results.Problem("Stored feature catalog failed validation.", statusCode: 422);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem("Stored feature catalog is unreadable.", statusCode: 422);
        }
    });

app.MapGet("/api/graphs/{project}/{revision}/features/{featureId}",
    (GraphSnapshotStore graphs, FeatureCatalogStore features,
     string project, string revision, string featureId) =>
    {
        if (string.IsNullOrWhiteSpace(featureId) || featureId.Length > 64)
            return Results.BadRequest(new { error = "Exact feature slug required." });
        var graph = graphs.Load(project, revision);
        if (graph is null) return Results.NotFound();
        try
        {
            var catalog = features.Load(graph);
            return catalog is null ? Results.NotFound()
                : Results.Ok(FeatureAtlas.Project(graph, catalog, featureId));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (InvalidDataException)
        {
            return Results.Problem("Stored feature catalog failed validation.", statusCode: 422);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem("Stored feature catalog is unreadable.", statusCode: 422);
        }
    });

// Read-only S31 evidence projection. No MSBuild evaluation or tests are triggered
// by a request, and unavailable indexing is NOT treated as zero mapped tests.
app.MapGet("/api/graphs/{project}/{revision}/test-links",
    (GraphSnapshotStore graphs, StaticTestAssociationStore reports,
     string project, string revision, string symbolId) =>
    {
        if (string.IsNullOrWhiteSpace(symbolId) || symbolId.Length > 2048)
            return Results.BadRequest(new { error = "Exact source SymbolId is required." });
        var graph = graphs.Load(project, revision);
        if (graph is null) return Results.NotFound();
        if (!graph.Nodes.Any(n => n.SymbolId == symbolId && n.Kind == GraphNodeKind.Method))
            return Results.NotFound();
        try
        {
            var report = reports.Load(graph);
            var links = report?.ForTarget(symbolId);
            return Results.Ok(new
            {
                schema = "archpulse.method-test-selection/v1",
                project, revision, targetSymbolId = symbolId,
                mappingStatus = report is null ? "NotIndexed" :
                    links!.Count == 0 ? "Unmapped" : "MappedDirectStatic",
                mappingScope = "RoslynStaticDirectOnly",
                runnerDiscovery = "NotRunnerDiscovered",
                executionStatus = "Unknown",
                observedCoverage = false,
                links = links ?? []
            });
        }
        catch (InvalidDataException)
        {
            return Results.Problem("Stored test-link evidence failed validation.", statusCode: 422);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem("Stored test-link evidence is unreadable.", statusCode: 422);
        }
    });

app.MapGet("/api/graphs/{project}/{revision}/neighbors",
    (GraphSnapshotStore store, string project, string revision, string symbolId) =>
    {
        var graph = store.Load(project, revision);
        if (graph is null) return Results.NotFound();
        try { return Results.Ok(GraphQueries.Neighbors(graph, symbolId)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapGet("/api/graphs/{project}/{revision}/path",
    (GraphSnapshotStore store, string project, string revision, string from, string to) =>
    {
        var graph = store.Load(project, revision);
        if (graph is null) return Results.NotFound();
        try { return Results.Ok(GraphQueries.ShortestStaticPath(graph, from, to, maxVisited: 5000)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (InvalidOperationException) { return Results.Problem("Traversal safety limit reached.", statusCode: 422); }
    });

// T025/S05: structural candidate feature impact only. This does not inspect
// Git patch contents or prove executed business effects of a proposed PR.
app.MapGet("/api/graphs/{project}/feature-impact",
    (GraphSnapshotStore graphs, FeatureCatalogStore features,
     string project, string from, string to) =>
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) ||
            from.Length > 160 || to.Length > 160)
            return Results.BadRequest(new { error = "Two exact bounded revisions are required." });
        var before = graphs.Load(project, from);
        var after = graphs.Load(project, to);
        if (before is null || after is null) return Results.NotFound();
        try
        {
            var catalog = features.Load(before);
            if (catalog is null)
                return Results.Ok(new
                {
                    schema = FeatureImpactAnalyzer.Schema, project, fromRevision = from,
                    toRevision = to, catalogStatus = "NotIndexed",
                    evidenceScope = FeatureImpactAnalyzer.Scope,
                    runtimeStatus = "Unknown", impacts = Array.Empty<FeatureImpactSignal>()
                });
            var report = FeatureImpactAnalyzer.Assess(before, after, catalog);
            if (report.Impacts.Count > 300)
                return Results.Problem("Candidate feature impact exceeds local response bounds.",
                    statusCode: 413);
            return Results.Ok(report);
        }
        catch (InvalidDataException)
        {
            return Results.Problem("Feature impact inputs or provenance could not be validated.",
                statusCode: 422);
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Problem("Stored feature mapping is unreadable.", statusCode: 422);
        }
    });

app.MapGet("/api/graphs/{project}/diff",
    (GraphSnapshotStore store, string project, string from, string to) =>
    {
        var previous = store.Load(project, from);
        var current = store.Load(project, to);
        if (previous is null || current is null) return Results.NotFound();
        return Results.Ok(GraphSnapshotStore.Compare(previous, current));
    });

app.MapPost("/api/guardian/evaluate",
    (GraphSnapshotStore store, GuardianEvaluationRequest request) =>
    {
        if (request.Rules is null || request.Rules.Length == 0 || request.Rules.Length > 100)
            return Results.BadRequest(new { Error = "Supply between 1 and 100 explicit rules." });
        var graph = store.Load(request.Project, request.Revision);
        if (graph is null) return Results.NotFound();
        return Results.Ok(ArchitectureGuardian.Evaluate(graph, request.Rules));
    });

// No graph mutation, arbitrary command execution, untrusted indexing,
// AI calls, CORS wildcard, public bind or bypass endpoint.
app.Run();

public sealed record GuardianEvaluationRequest(
    string Project, string Revision, ForbiddenDependencyRule[] Rules);

public partial class Program { }
