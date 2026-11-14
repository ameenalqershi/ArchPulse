// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;

internal static class CrossProjectDataJourneyEndpoints
{
    internal static IResult Get(GraphSnapshotStore graphs, CrossProjectDataJourneyStore journeys,
        string project, string revision, string? symbolId, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(symbolId) || symbolId.Length > 2048)
            return Results.BadRequest();
        try
        {
            var graph = graphs.Load(project, revision);
            if (graph is null || !graph.Nodes.Any(node =>
                node.Kind == GraphNodeKind.Method && node.SymbolId == symbolId))
                return Results.NotFound();

            var report = journeys.Load(graph);
            var findings = report?.Findings.Where(flow => flow.IncludesMethod(symbolId)).ToArray() ?? [];
            return Results.Ok(new
            {
                schema = "archpulse.datajourney-method-selection/v1",
                project,
                revision,
                symbolId,
                evidence = EvidenceKind.Static,
                runtimeStatus = "Unknown",
                status = report is null ? "NotIndexed" : findings.Length > 0 ? "Mapped" :
                    report.Truncated ? "Indeterminate" : "NoWitness",
                truncated = report?.Truncated ?? false,
                findings
            });
        }
        catch (Exception error) when (error is InvalidDataException or JsonException or EndOfStreamException)
        {
            return Results.Problem("Stored DataJourney evidence failed validation.", statusCode: 422);
        }
    }
}
