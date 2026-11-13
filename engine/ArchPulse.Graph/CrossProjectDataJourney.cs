// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchPulse.Graph;

public enum DataJourneyHopKind { Argument, Return }

public enum DataJourneyBoundary
{
    AnnotatedSink, OpaqueCall, UnsupportedSyntax, DeclaredSanitizer, AnalysisLimit
}

public sealed record DataJourneyHop(DataJourneyHopKind Kind, GraphEdge Call)
{
    [JsonIgnore]
    public string From => Kind == DataJourneyHopKind.Argument ? Call.FromSymbolId : Call.ToSymbolId;
    [JsonIgnore]
    public string To => Kind == DataJourneyHopKind.Argument ? Call.ToSymbolId : Call.FromSymbolId;
}

public sealed record CrossProjectDataFlow(
    string SourceMethodSymbolId, SourceEvidence Source, IReadOnlyList<DataJourneyHop> Hops,
    DataJourneyBoundary Boundary, SourceEvidence Destination)
{
    public bool IncludesMethod(string id) =>
        SourceMethodSymbolId == id || Hops.Any(hop => hop.From == id || hop.To == id);
}

public sealed record CrossProjectDataJourneyReport(
    string Schema, string Project, string Revision, EvidenceKind Evidence,
    string RuntimeStatus, bool Truncated, IReadOnlyList<CrossProjectDataFlow> Findings);

/// <summary>Stores source-only reports separately from the canonical graph.</summary>
public sealed class CrossProjectDataJourneyStore(string directory)
{
    public const string Schema = "archpulse.datajourney-cross-project/v1";
    public const int MaxFindings = 512;
    public const int MaxHops = 16;
    private const int MaxBytes = 8 * 1024 * 1024;
    private readonly string _directory = Path.GetFullPath(directory);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public void Save(GraphSnapshot graph, CrossProjectDataJourneyReport report)
    {
        Validate(graph, report);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(report, Options);
        if (bytes.Length > MaxBytes)
            throw new InvalidDataException("Cross-project DataJourney report exceeds its size limit.");
        Directory.CreateDirectory(_directory);
        string path = PathFor(graph);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            try { File.Move(temporary, path, overwrite: false); }
            catch (IOException) when (File.Exists(path))
            {
                if (!ReadBounded(path).AsSpan().SequenceEqual(bytes))
                    throw new InvalidOperationException("Cross-project DataJourney is immutable at the stored revision.");
            }
        }
        finally { File.Delete(temporary); }
    }

    public CrossProjectDataJourneyReport? Load(GraphSnapshot graph)
    {
        byte[] data;
        try { data = ReadBounded(PathFor(graph)); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        var report = JsonSerializer.Deserialize<CrossProjectDataJourneyReport>(data, Options);
        if (report is null) throw new InvalidDataException("Missing cross-project DataJourney report.");
        Validate(graph, report);
        return report;
    }

    private static void Validate(GraphSnapshot graph, CrossProjectDataJourneyReport report)
    {
        GraphSnapshotStore.Validate(graph);
        if (report.Schema != Schema || report.Project != graph.Project || report.Revision != graph.Revision ||
            report.Evidence != EvidenceKind.Static || report.RuntimeStatus != "Unknown" ||
            report.Findings is null || report.Findings.Count > MaxFindings)
            throw InvalidWitness();

        var nodes = graph.Nodes.ToDictionary(n => n.SymbolId, StringComparer.Ordinal);
        var edges = graph.Edges.ToDictionary(e => e.EdgeId, StringComparer.Ordinal);
        foreach (var flow in report.Findings)
        {
            if (flow is null || flow.SourceMethodSymbolId is null ||
                !nodes.TryGetValue(flow.SourceMethodSymbolId, out var source) ||
                source.Kind != GraphNodeKind.Method || !Enum.IsDefined(flow.Boundary) ||
                !ValidLocation(flow.Source, graph.Revision) || !ValidLocation(flow.Destination, graph.Revision) ||
                flow.Source.RelativeFile != source.Evidence.RelativeFile ||
                flow.Source.Line < source.Evidence.Line ||
                flow.Hops is null || flow.Hops.Count > MaxHops)
                throw InvalidWitness();

            string current = flow.SourceMethodSymbolId;
            foreach (var hop in flow.Hops)
            {
                if (hop is null || !Enum.IsDefined(hop.Kind) || hop.Call?.EdgeId is not { } id ||
                    !edges.TryGetValue(id, out var edge) || edge != hop.Call || edge.Relation != "Calls" ||
                    !ValidLocation(edge.Evidence, graph.Revision) ||
                    current != hop.From || nodes[hop.To].Kind != GraphNodeKind.Method)
                    throw InvalidWitness();
                current = hop.To;
            }

            if (flow.Boundary == DataJourneyBoundary.AnnotatedSink &&
                (flow.Hops.LastOrDefault() is not { Kind: DataJourneyHopKind.Argument } final ||
                 final.Call.Evidence != flow.Destination))
                throw InvalidWitness();
        }
    }

    private static bool ValidLocation(SourceEvidence? location, string revision) =>
        location is { Kind: EvidenceKind.Static, Line: > 0, Column: > 0 } &&
        location.Revision == revision &&
        location.RelativeFile is { Length: > 0 and <= 1024 } file &&
        !file.StartsWith('/') && !file.Contains('\\') && !file.Contains(':') &&
        !file.Split('/').Contains("..") && !file.Any(char.IsControl);

    private static InvalidDataException InvalidWitness() =>
        new("Cross-project DataJourney does not match its static graph.");

    private string PathFor(GraphSnapshot graph)
    {
        var key = Encoding.UTF8.GetBytes(Schema + "\0" + graph.Project + "\0" + graph.Revision);
        return Path.Combine(_directory, Convert.ToHexStringLower(SHA256.HashData(key)) + ".datajourney-cross-project.json");
    }

    private static byte[] ReadBounded(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length is < 1 or > MaxBytes)
            throw new InvalidDataException("Cross-project DataJourney has invalid size.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("Cross-project DataJourney changed during reading.");
        return bytes;
    }
}
