// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchPulse.Graph;

/// <summary>
/// Immutable, local-only sidecar for source-backed method-to-test declarations.
/// Fails closed if identity, call-site evidence or source graph does not match.
/// This does not store executed test cases, runner results or runtime traces.
/// </summary>
public sealed class StaticTestAssociationStore
{
    public const long MaxDocumentBytes = 8 * 1024 * 1024;
    private const int MaxDeclarations = 20_000;
    private const int MaxAssociations = 50_000;
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public StaticTestAssociationStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A local store is required.", nameof(directory));
        _root = Path.GetFullPath(directory);
    }

    public string Save(GraphSnapshot graph, StaticTestAssociationReport report)
    {
        Validate(graph, report);
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(report, JsonOptions);
        if (data.Length > MaxDocumentBytes)
            throw new InvalidDataException("Test-link document exceeds the bounded 8 MiB store.");

        Directory.CreateDirectory(_root);
        string dest = PathFor(graph.Project, graph.Revision);
        if (File.Exists(dest))
        {
            EnsureIdentical(dest, data);
            return dest;
        }

        string temp = Path.Combine(_root, ".ap-links-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            try { File.Move(temp, dest, overwrite: false); }
            catch (IOException) when (File.Exists(dest)) { EnsureIdentical(dest, data); }
            return dest;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public StaticTestAssociationReport? Load(GraphSnapshot graph)
    {
        GraphSnapshotStore.Validate(graph);
        string file = PathFor(graph.Project, graph.Revision);
        if (!File.Exists(file)) return null;
        var info = new FileInfo(file);
        if (info.Length == 0 || info.Length > MaxDocumentBytes)
            throw new InvalidDataException("Stored test-link document has invalid size.");
        byte[] raw = File.ReadAllBytes(file);
        if (raw.LongLength > MaxDocumentBytes)
            throw new InvalidDataException("Stored test-link document exceeds the byte budget.");
        var report = JsonSerializer.Deserialize<StaticTestAssociationReport>(raw, JsonOptions)
            ?? throw new InvalidDataException("Stored test-link document is empty.");
        Validate(graph, report);
        return report;
    }

    public static void Validate(GraphSnapshot graph, StaticTestAssociationReport report)
    {
        GraphSnapshotStore.Validate(graph);
        ArgumentNullException.ThrowIfNull(report);
        if (report.Schema != "archpulse.method-test-associations/v1" ||
            report.GraphProject != graph.Project || report.GraphRevision != graph.Revision ||
            report.MappingScope != "RoslynStaticDirectOnly" ||
            report.RunnerDiscovery != "NotRunnerDiscovered" ||
            report.ResultEvidence != "NoRunOrCoverageReceipts" ||
            report.TestDeclarations is null || report.Associations is null || report.Diagnostics is null ||
            report.TestDeclarations.Count > MaxDeclarations || report.Associations.Count > MaxAssociations ||
            report.Diagnostics.Count > 5_000)
            throw new InvalidDataException("Unsupported, mismatched or unbounded test-link report.");

        var nodes = graph.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => e.Relation == "Calls" && e.Evidence.Kind == EvidenceKind.Static)
            .ToLookup(e => (e.FromSymbolId, e.ToSymbolId));
        var discovered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var test in report.TestDeclarations)
        {
            if (test is null || !discovered.Add(test.TestMethodSymbolId) ||
                !nodes.TryGetValue(test.TestMethodSymbolId, out var node) ||
                node.Kind != GraphNodeKind.Method || node.Evidence != test.Evidence ||
                !test.TestMethodSymbolId.StartsWith(test.TestProject + "::", StringComparison.Ordinal) ||
                !nodes.ContainsKey("project:" + test.TestProject) ||
                test.Framework is not ("xUnit" or "NUnit" or "MSTest") ||
                test.DiscoveryEvidence != "KnownAttributeNameUnverifiedByRunner" ||
                test.CaseGranularity != "TestMethodDeclarationNotParameterizedCase" ||
                test.LastResult != "NotRun")
                throw new InvalidDataException("Invalid test declaration source or provenance.");
        }

        var associations = new HashSet<(string Test, string Target)>();
        foreach (var link in report.Associations)
        {
            if (link is null || !discovered.Contains(link.TestMethodSymbolId) ||
                !nodes.TryGetValue(link.TargetSymbolId, out var target) ||
                target.Kind != GraphNodeKind.Method ||
                !associations.Add((link.TestMethodSymbolId, link.TargetSymbolId)) ||
                !link.TestMethodSymbolId.StartsWith(link.TestProject + "::", StringComparison.Ordinal) ||
                link.TargetSymbolId.StartsWith(link.TestProject + "::", StringComparison.Ordinal) ||
                !report.TestDeclarations.Any(t => t.TestMethodSymbolId == link.TestMethodSymbolId &&
                                                   t.Framework == link.Framework &&
                                                   t.TestProject == link.TestProject) ||
                link.Relation != "DirectInvocation" || link.Verification != "StaticSourceOnly" ||
                link.ExecutionEvidence != "Unobserved" || link.LastResult != "Unknown" ||
                link.CallSites is null || link.CallSites.Count is < 1 or > 1000)
                throw new InvalidDataException("Invalid direct static test association.");
            var available = edges[(link.TestMethodSymbolId, link.TargetSymbolId)]
                .Select(e => e.Evidence).ToHashSet();
            var unique = new HashSet<SourceEvidence>();
            foreach (var evidence in link.CallSites)
                if (evidence is null || evidence.Kind != EvidenceKind.Static ||
                    evidence.Revision != graph.Revision ||
                    !available.Contains(evidence) || !unique.Add(evidence))
                    throw new InvalidDataException("Association call site is absent or non-static.");
        }
    }

    private string PathFor(string project, string revision)
    {
        if (string.IsNullOrWhiteSpace(project) || string.IsNullOrWhiteSpace(revision))
            throw new ArgumentException("Exact project and revision are required.");
        byte[] id = Encoding.UTF8.GetBytes(project + "\0" + revision);
        return Path.Combine(_root, Convert.ToHexStringLower(SHA256.HashData(id)) + ".test-links.json");
    }

    private static void EnsureIdentical(string path, byte[] expected)
    {
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
            throw new InvalidOperationException("Test links for this revision are immutable.");
    }
}
