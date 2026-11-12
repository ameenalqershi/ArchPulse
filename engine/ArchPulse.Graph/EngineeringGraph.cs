// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using ArchPulse.Core;

namespace ArchPulse.Graph;

public enum GraphNodeKind { Repository, Solution, Project, Namespace, Class, Interface, Struct, Record, Method, Property, ExternalDependency }
public enum GraphEdgeKind { Contains, Calls, Implements, DependsOn }

public sealed record SourceSpan(string File, int StartLine, int StartColumn, int EndLine, int EndColumn);

/// <summary>Provenance is a claim classification, not independent authenticity proof.</summary>
public sealed record GraphProvenance(
    string Revision, EvidenceKind Kind, string Analyzer, string? SymbolKey, SourceSpan? Source);

public sealed record GraphNode(string Id, GraphNodeKind Kind, string Name, GraphProvenance Evidence);
public sealed record GraphEdge(string Id, string From, string To, GraphEdgeKind Kind, GraphProvenance Evidence);

/// <summary>One immutable revision snapshot; unresolved work must be exposed as diagnostics.</summary>
public sealed record GraphSnapshot(
    string Revision, IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<string> VerifyIntegrity()
    {
        var errors = new List<string>();
        var ids = Nodes.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != Nodes.Count) errors.Add("Duplicate node IDs");
        if (Edges.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Edges.Count)
            errors.Add("Duplicate edge IDs");
        foreach (var edge in Edges)
        {
            if (!ids.Contains(edge.From) || !ids.Contains(edge.To))
                errors.Add($"Orphan edge {edge.Id}");
            if (edge.Evidence.Revision != Revision)
                errors.Add($"Stale edge revision {edge.Id}");
        }
        if (Nodes.Any(n => n.Evidence.Revision != Revision)) errors.Add("Stale node revision");
        return errors;
    }

    public string ToMermaid()
    {
        var builder = new StringBuilder("flowchart LR\n");
        foreach (var node in Nodes.OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var name = node.Name.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);
            builder.Append("    ").Append(node.Id).Append("[\"").Append(name).Append("\"]\n");
        }
        foreach (var edge in Edges.OrderBy(e => e.Id, StringComparer.Ordinal))
            builder.Append("    ").Append(edge.From).Append(" -->|")
                .Append(edge.Kind.ToString()).Append("| ").Append(edge.To).Append('\n');
        return builder.ToString();
    }
}

public sealed class GraphBuilder(string revision)
{
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphEdge> _edges = new(StringComparer.Ordinal);
    private readonly List<string> _diagnostics = [];

    public void AddNode(GraphNode node)
    {
        if (node.Evidence.Revision != revision) throw new ArgumentException("Revision mismatch");
        _nodes.TryAdd(node.Id, node);
    }
    public void AddEdge(string from, string to, GraphEdgeKind kind, GraphProvenance evidence)
    {
        if (!_nodes.ContainsKey(from) || !_nodes.ContainsKey(to))
            throw new InvalidOperationException("Edges must reference indexed nodes.");
        var id = StableId("edge", $"{from}:{kind}:{to}");
        _edges.TryAdd(id, new GraphEdge(id, from, to, kind, evidence));
    }
    public void AddDiagnostic(string message) => _diagnostics.Add(message);
    public GraphSnapshot Build() => new(revision,
        _nodes.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
        _edges.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
        _diagnostics.ToArray());

    public static string StableId(string prefix, string canonical)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return prefix + Convert.ToHexStringLower(digest);
    }
}
