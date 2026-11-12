// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

public enum EvidenceKind
{
    Designed,
    Static,
    Observed,
    Inferred,
    Verified,
    Unknown
}

public enum GraphNodeKind
{
    Project,
    Namespace,
    Class,
    Interface,
    Struct,
    Record,
    Enum,
    Method,
    Property
}

public sealed record SourceEvidence(
    string Revision,
    string RelativeFile,
    int Line,
    int Column,
    EvidenceKind Kind);

public sealed record GraphNode(
    string SymbolId,
    GraphNodeKind Kind,
    string Label,
    string? ParentSymbolId,
    SourceEvidence Evidence);

public sealed record GraphEdge(
    string EdgeId,
    string FromSymbolId,
    string ToSymbolId,
    string Relation,
    SourceEvidence Evidence);

public sealed record GraphSnapshot(
    string Project,
    string Revision,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<string> Diagnostics)
{
    public IEnumerable<GraphEdge> CallsFrom(string methodSymbolId) =>
        Edges.Where(edge => edge.Relation == "Calls" && edge.FromSymbolId == methodSymbolId);
}
