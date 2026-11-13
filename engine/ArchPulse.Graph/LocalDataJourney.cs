// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchPulse.Graph;

/// <summary>
/// A heuristic source-only sink classification. 'NoCandidateInSubset' never
/// proves privacy safety, and a declared sanitizer has NOT been validated.
/// </summary>
public enum LocalFlowOutcome
{
    NoCandidateInSubset,
    StaticPossibleRisk,
    DeclaredSanitizerNeedsReview,
    Unknown,
    StaticReturnPropagation
}

public enum LocalFlowDestination { DeclaredSink, MethodReturn }

public sealed record LocalDataFlowFinding(
    string MethodSymbolId, string DestinationSymbolId,
    LocalFlowDestination Destination, LocalFlowOutcome Outcome,
    string? SourceSymbolId, SourceEvidence Location);

public sealed record LocalDataJourneyReport(
    string Schema, string Project, string Revision,
    string Scope, string RuntimeStatus,
    IReadOnlyList<LocalDataFlowFinding> Findings,
    IReadOnlyList<string> Diagnostics);

public static class LocalDataJourneyContract
{
    public const string Schema = "archpulse.datajourney-local/v1";
    public const string Scope =
        "ExplicitLocalAnnotations;StraightLineOnly;NoValuesStored;NotSecurityProof";
    public const string RuntimeStatus = "Unknown";
    public static void Validate(GraphSnapshot graph, LocalDataJourneyReport report)
    {
        GraphSnapshotStore.Validate(graph);
        if (report is null || report.Schema != Schema ||
            report.Project != graph.Project || report.Revision != graph.Revision ||
            report.Scope != Scope || report.RuntimeStatus != RuntimeStatus ||
            report.Findings is null || report.Findings.Count > 3000 ||
            report.Diagnostics is null || report.Diagnostics.Count > 500)
            throw new InvalidDataException("DataJourney report identity, scope or size is invalid.");
        var methods = graph.Nodes.Where(x => x.Kind == GraphNodeKind.Method)
            .Select(x => x.SymbolId).ToHashSet(StringComparer.Ordinal);
        foreach (var f in report.Findings)
        {
            if (f is null || !Enum.IsDefined(f.Destination) || !Enum.IsDefined(f.Outcome) ||
                !methods.Contains(f.MethodSymbolId) ||
                string.IsNullOrWhiteSpace(f.DestinationSymbolId) ||
                f.DestinationSymbolId.Length > 800 ||
                f.SourceSymbolId is { Length: > 800 } ||
                f.Location is null || f.Location.Kind != EvidenceKind.Static ||
                f.Location.Revision != graph.Revision ||
                string.IsNullOrWhiteSpace(f.Location.RelativeFile) ||
                f.Location.RelativeFile.Length > 1024 || f.Location.Line < 1 ||
                f.Location.Column < 1 ||
                f.Location.RelativeFile.Any(char.IsControl) ||
                (f.Destination == LocalFlowDestination.MethodReturn &&
                 f.Outcome is not (LocalFlowOutcome.StaticReturnPropagation or LocalFlowOutcome.Unknown)) ||
                (f.Destination == LocalFlowDestination.DeclaredSink &&
                 f.Outcome == LocalFlowOutcome.StaticReturnPropagation))
                throw new InvalidDataException("DataJourney witness is inconsistent with exact static graph.");
        }
        foreach (var diagnostic in report.Diagnostics)
            if (string.IsNullOrWhiteSpace(diagnostic) || diagnostic.Length > 350 ||
                diagnostic.Any(char.IsControl))
                throw new InvalidDataException("Invalid DataJourney diagnostic.");
    }
}

/// <summary>Immutable exact-project/revision sidecar; append-only source semantics, no payload values.</summary>
public sealed class LocalDataJourneyStore
{
    private const int MaxBytes = 4 * 1024 * 1024;
    private readonly string _directory;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public LocalDataJourneyStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A trusted local store directory is required.");
        _directory = Path.GetFullPath(directory);
    }

    public string Save(GraphSnapshot graph, LocalDataJourneyReport report)
    {
        LocalDataJourneyContract.Validate(graph, report);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, Options);
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException("DataJourney report exceeds 4 MiB.");
        Directory.CreateDirectory(_directory);
        var path = PathFor(graph.Project, graph.Revision);
        if (File.Exists(path)) { CompareBytes(path, bytes); return path; }
        var temp = Path.Combine(_directory, ".datajourney-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(true);
            }
            try { File.Move(temp, path, overwrite: false); }
            catch (IOException) when (File.Exists(path)) { CompareBytes(path, bytes); }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return path;
    }

    public LocalDataJourneyReport? Load(GraphSnapshot graph)
    {
        GraphSnapshotStore.Validate(graph);
        var path = PathFor(graph.Project, graph.Revision);
        if (!File.Exists(path)) return null;
        var length = new FileInfo(path).Length;
        if (length is < 1 or > MaxBytes)
            throw new InvalidDataException("DataJourney sidecar has invalid size.");
        var report = JsonSerializer.Deserialize<LocalDataJourneyReport>(File.ReadAllBytes(path), Options)
            ?? throw new InvalidDataException("DataJourney sidecar was empty.");
        LocalDataJourneyContract.Validate(graph, report);
        return report;
    }

    private string PathFor(string project, string revision)
    {
        var key = Encoding.UTF8.GetBytes("datajourney-local-v1\0" + project + "\0" + revision);
        return Path.Combine(_directory,
            Convert.ToHexStringLower(SHA256.HashData(key)) + ".datajourney.json");
    }
    private static void CompareBytes(string path, byte[] expected)
    {
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
            throw new InvalidOperationException("DataJourney report for the revision is immutable.");
    }
}
