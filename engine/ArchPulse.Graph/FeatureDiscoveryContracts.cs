// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArchPulse.Graph;

public enum FeatureSignalKind { ControllerAction, NamedHandler, TestDeclaration }

/// <summary>
/// A source-derived DETECTOR SIGNAL, not a confirmed business-feature mapping.
/// The symbol/evidence refers to an existing graph declaration; inferred business
/// meaning must never be elevated to Static, Observed or Verified.
/// </summary>
public sealed record FeatureCandidateSignal(
    string SignalId, FeatureSignalKind Kind, string SymbolId, string SuggestedFeatureId,
    string Detector, string Explanation, SourceEvidence Source,
    EvidenceKind BusinessClaim, string? HttpVerb, string? RouteFragment);

public sealed record FeatureDiscoveryReport(
    string Schema, string GraphProject, string GraphRevision, string DiscoveryScope,
    string RuntimeStatus, IReadOnlyList<FeatureCandidateSignal> Signals,
    IReadOnlyList<string> Diagnostics);

public static partial class FeatureDiscoveryContract
{
    public const string Schema = "archpulse.feature-discovery/v1";
    public const string Scope = "SourceSignalsOnly;HumanReviewRequired;NotFeatureMembership";
    public const string RuntimeStatus = "Unknown";

    [GeneratedRegex("^[a-z][a-z0-9-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex Slug();

    public static void Validate(GraphSnapshot graph, FeatureDiscoveryReport report)
    {
        GraphSnapshotStore.Validate(graph);
        if (report is null || report.Schema != Schema || report.GraphProject != graph.Project ||
            report.GraphRevision != graph.Revision || report.DiscoveryScope != Scope ||
            report.RuntimeStatus != RuntimeStatus ||
            report.Signals is null || report.Signals.Count > 5_000 ||
            report.Diagnostics is null || report.Diagnostics.Count > 500)
            throw new InvalidDataException("Feature discovery report has invalid identity, scope or bounds.");
        var methods = graph.Nodes.Where(n => n.Kind == GraphNodeKind.Method)
            .ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var signal in report.Signals)
        {
            if (signal is null || !Enum.IsDefined(signal.Kind) ||
                string.IsNullOrWhiteSpace(signal.SymbolId) ||
                signal.SignalId != $"feature-signal:{signal.Kind}:{signal.SymbolId}" ||
                !seen.Add(signal.SignalId) ||
                !methods.TryGetValue(signal.SymbolId, out var method) ||
                method.Evidence != signal.Source || signal.Source.Kind != EvidenceKind.Static ||
                signal.Source.Revision != report.GraphRevision ||
                signal.BusinessClaim != EvidenceKind.Inferred ||
                signal.SuggestedFeatureId is null || !Slug().IsMatch(signal.SuggestedFeatureId) ||
                signal.Explanation is null || signal.Explanation.Length is < 1 or > 500 ||
                signal.Explanation.Any(char.IsControl))
                throw new InvalidDataException("Feature signal lacks exact source evidence or invents business proof.");
            var expected = signal.Kind switch
            {
                FeatureSignalKind.ControllerAction => "KnownMvcHttpAttributeName;NotRegisteredRoute",
                FeatureSignalKind.NamedHandler => "HandlerTypeAndMethodNaming;NotRuntimeRegistration",
                FeatureSignalKind.TestDeclaration => "KnownTestAttributeName;NotRunnerDiscovered",
                _ => ""
            };
            if (signal.Detector != expected)
                throw new InvalidDataException("Unsupported feature discovery detector.");
            if (signal.Kind == FeatureSignalKind.ControllerAction)
            {
                if (signal.HttpVerb is not ("GET" or "POST" or "PUT" or "DELETE" or "PATCH") ||
                    signal.RouteFragment is not null &&
                    (signal.RouteFragment.Length > 256 || signal.RouteFragment.Any(char.IsControl)))
                    throw new InvalidDataException("Unsupported route signal details.");
            }
            else if (signal.HttpVerb is not null || signal.RouteFragment is not null)
                throw new InvalidDataException("Only controller actions can carry an HTTP hint.");
        }
        foreach (var diagnostic in report.Diagnostics)
            if (diagnostic is null || diagnostic.Length > 500 || diagnostic.Any(char.IsControl))
                throw new InvalidDataException("Invalid feature discovery diagnostic.");
    }
}

/// <summary>Immutable sidecar for trust-local discovery; not a signed provenance attestation.</summary>
public sealed class FeatureDiscoveryStore
{
    private const int MaxBytes = 4 * 1024 * 1024;
    private readonly string _directory;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public FeatureDiscoveryStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A local graph store path is required.", nameof(directory));
        _directory = Path.GetFullPath(directory);
    }

    public string Save(GraphSnapshot graph, FeatureDiscoveryReport report)
    {
        FeatureDiscoveryContract.Validate(graph, report);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(report, Options);
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException("Discovery report exceeds 4 MiB.");
        Directory.CreateDirectory(_directory);
        var path = PathFor(graph.Project, graph.Revision);
        if (File.Exists(path))
        {
            EnsureSame(path, bytes);
            return path;
        }
        var temp = Path.Combine(_directory, ".feature-signals-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            try { File.Move(temp, path, overwrite: false); }
            catch (IOException) when (File.Exists(path)) { EnsureSame(path, bytes); }
            return path;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public FeatureDiscoveryReport? Load(GraphSnapshot graph)
    {
        GraphSnapshotStore.Validate(graph);
        var path = PathFor(graph.Project, graph.Revision);
        if (!File.Exists(path)) return null;
        var info = new FileInfo(path);
        if (info.Length is < 1 or > MaxBytes)
            throw new InvalidDataException("Stored feature discovery has an invalid size.");
        var report = JsonSerializer.Deserialize<FeatureDiscoveryReport>(File.ReadAllBytes(path), Options)
            ?? throw new InvalidDataException("Stored discovery report is empty.");
        FeatureDiscoveryContract.Validate(graph, report);
        return report;
    }

    private string PathFor(string project, string revision)
    {
        byte[] key = Encoding.UTF8.GetBytes("feature-discovery-v1\0" + project + "\0" + revision);
        return Path.Combine(_directory,
            Convert.ToHexStringLower(SHA256.HashData(key)) + ".feature-signals.json");
    }

    private static void EnsureSame(string path, byte[] bytes)
    {
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            throw new InvalidOperationException("Discovery report for exact graph revision is immutable.");
    }
}
