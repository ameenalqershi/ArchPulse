// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using ArchPulse.Graph;

internal sealed class DataJourneyReview(CrossProjectDataJourneyReport report, DataJourneyPolicy policy)
{
    private readonly DataJourneyPolicyEvaluator _evaluator = new(policy);

    public void WriteSummary(TextWriter output, JsonSerializerOptions options)
    {
        var findings = report.Findings.Select(_evaluator.Evaluate).ToArray();
        var summary = new
        {
            Schema = "archpulse.datajourney-review-summary/v1",
            report.Evidence,
            report.RuntimeStatus,
            report.Truncated,
            Findings = findings.Length,
            PolicyOutcomes = Enum.GetValues<ReviewDisposition>().ToDictionary(
                disposition => disposition.ToString(),
                disposition => findings.Count(f => f.Disposition == disposition)),
            Boundaries = Enum.GetValues<DataJourneyBoundary>().ToDictionary(
                boundary => boundary.ToString(),
                boundary => findings.Count(f => f.Flow.Boundary == boundary))
        };
        output.WriteLine(JsonSerializer.Serialize(summary, options));
    }

    public void WriteHtml(TextWriter output)
    {
        output.WriteLine("""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
            <title>DataJourney review</title>
            <style>
            html{color-scheme:light dark}
            body{font:1rem/1.5 system-ui,sans-serif;margin:auto;max-width:66rem;padding:1.25rem}
            h1{margin-bottom:.4rem}
            code{overflow-wrap:anywhere}
            details{border:1px solid;padding:.75rem;margin-block:.65rem;border-radius:.5rem}
            summary{cursor:pointer}
            li{margin-block:.65rem}
            </style></head><body><main>
            <h1>DataJourney review</h1>
            """);
        output.WriteLine($"<p>{Html(report.Project)} · revision <code>{Html(report.Revision)}</code></p>");
        output.WriteLine("<p><strong>Static possible.</strong> Runtime evidence: <strong>Unknown</strong>.</p>");
        if (report.Truncated)
            output.WriteLine("<p><strong>Analysis truncated.</strong> Additional paths may be missing.</p>");
        if (report.Findings.Count == 0)
            output.WriteLine("<p>The supported analysis returned no findings.</p>");

        foreach (var review in report.Findings.Select(_evaluator.Evaluate))
        {
            var flow = review.Flow;
            output.WriteLine($"<details><summary>{flow.Boundary} · {review.Disposition} · " +
                $"<code>{Html(flow.SourceMethodSymbolId)}</code></summary>");
            output.WriteLine($"<p>Source classification: {review.Classification?.ToString() ?? "Unknown"}</p>");
            output.WriteLine($"<ol><li><strong>Source</strong> {Location(flow.Source)}</li>");
            foreach (var hop in review.Hops)
            {
                output.WriteLine($"<li><strong>{hop.Hop.Kind}</strong> " +
                    $"<code>{Html(hop.From)}</code> → <code>{Html(hop.To)}</code><br>" +
                    $"{Location(hop.Hop.Call.Evidence)} · {Html(hop.FromZone ?? "Unknown")} → " +
                    $"{Html(hop.ToZone ?? "Unknown")} · {hop.Disposition}</li>");
            }
            output.WriteLine($"<li><strong>{flow.Boundary}</strong> {Location(flow.Destination)}</li></ol></details>");
        }
        output.WriteLine("</main></body></html>");
    }

    private static string Location(SourceEvidence evidence) =>
        $"<code>{Html(evidence.RelativeFile)}:{evidence.Line}:{evidence.Column}</code>";

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
