// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;

namespace ArchPulse.Guardian;

public enum RuleSeverity { Warning, Error, Blocker }
public enum RuleOutcome { Violation, Waived, Unknown, NotEvaluated }

/// <summary>
/// A policy prohibits one or more source-backed directed relations between concrete projects.
/// This first slice deliberately does NOT claim to detect indirect runtime execution.
/// </summary>
public sealed record ForbiddenDependencyRule(
    string Id, string Version, string FromProject, string ToProject,
    string Relation, RuleSeverity Severity, bool Enabled = true);

public sealed record HumanWaiver(
    string RuleId, string EdgeId, string Approver, string Justification, DateTimeOffset ExpiresAt);

public sealed record ArchitectureFinding(
    string RuleId, string RuleVersion, RuleSeverity Severity,
    RuleOutcome Outcome, string? EdgeId, string? FromSymbolId,
    string? ToSymbolId, SourceEvidence? Evidence, string Explanation);

public static class ArchitectureGuardian
{
    public static IReadOnlyList<ArchitectureFinding> Evaluate(
        GraphSnapshot snapshot,
        IEnumerable<ForbiddenDependencyRule> policies,
        IReadOnlyCollection<HumanWaiver>? waivers = null,
        DateTimeOffset? evaluationTime = null)
    {
        GraphSnapshotStore.Validate(snapshot);
        var rules = policies.ToArray();
        var now = evaluationTime ?? DateTimeOffset.UtcNow;
        var result = new List<ArchitectureFinding>();

        // Project labels select a policy scope only when the label identifies
        // exactly one canonical project node in this graph revision.
        var projectNameCounts = snapshot.Nodes
            .Where(node => node.Kind == GraphNodeKind.Project && !string.IsNullOrWhiteSpace(node.Label))
            .GroupBy(node => node.Label, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var owners = BuildProjectOwnerIndex(snapshot);

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.Version)
                || string.IsNullOrWhiteSpace(rule.FromProject) || string.IsNullOrWhiteSpace(rule.ToProject)
                || string.IsNullOrWhiteSpace(rule.Relation))
                throw new ArgumentException("Policy ID, version, project names and relation are required.");
            if (!projectNameCounts.TryGetValue(rule.FromProject, out int fromCount)
                || !projectNameCounts.TryGetValue(rule.ToProject, out int toCount))
            {
                result.Add(new ArchitectureFinding(rule.Id, rule.Version, rule.Severity,
                    RuleOutcome.NotEvaluated, null, null, null, null,
                    "One or both projects are absent from this revision; rule cannot be evaluated."));
                continue;
            }
            if (fromCount != 1 || toCount != 1)
            {
                result.Add(new ArchitectureFinding(rule.Id, rule.Version, rule.Severity,
                    RuleOutcome.NotEvaluated, null, null, null, null,
                    "One or both project names are ambiguous in this revision; rule cannot be evaluated."));
                continue;
            }

            var matches = snapshot.Edges.Where(edge =>
                edge.Relation == rule.Relation
                && string.Equals(owners[edge.FromSymbolId], rule.FromProject, StringComparison.Ordinal)
                && string.Equals(owners[edge.ToSymbolId], rule.ToProject, StringComparison.Ordinal)).ToArray();

            if (matches.Length == 0)
            {
                result.Add(new ArchitectureFinding(rule.Id, rule.Version, rule.Severity,
                    RuleOutcome.NotEvaluated, null, null, null, null,
                    "No matching static relation found. This does not prove the absence of dynamic calls."));
                continue;
            }

            foreach (var edge in matches)
            {
                var evidence = edge.Evidence;
                var trustworthy = evidence.Kind is EvidenceKind.Static or EvidenceKind.Observed or EvidenceKind.Verified;
                if (!trustworthy)
                {
                    result.Add(new ArchitectureFinding(rule.Id, rule.Version, rule.Severity,
                        RuleOutcome.Unknown, edge.EdgeId, edge.FromSymbolId, edge.ToSymbolId, evidence,
                        "Inferred or designed relation cannot establish a proven policy violation."));
                    continue;
                }

                var waiver = waivers?.FirstOrDefault(w =>
                    w.RuleId == rule.Id && w.EdgeId == edge.EdgeId
                    && !string.IsNullOrWhiteSpace(w.Approver)
                    && !string.IsNullOrWhiteSpace(w.Justification)
                    && w.ExpiresAt > now);

                result.Add(new ArchitectureFinding(rule.Id, rule.Version, rule.Severity,
                    waiver is null ? RuleOutcome.Violation : RuleOutcome.Waived,
                    edge.EdgeId, edge.FromSymbolId, edge.ToSymbolId, evidence,
                    waiver is null
                        ? $"Forbidden {edge.Relation} relation is supported by {evidence.Kind} evidence at " +
                          $"{evidence.RelativeFile}:{evidence.Line} ({snapshot.Revision})."
                        : $"Time-limited waiver by {waiver.Approver}; expires {waiver.ExpiresAt:O}."));
            }
        }
        return result.OrderBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.EdgeId, StringComparer.Ordinal).ToArray();
    }

    // Canonical hierarchy, not symbol-ID spelling, determines a relation's
    // owning project. Resolve each parent chain once: O(nodes), no recursion.
    // Validate() already forbids orphan parents and ownership cycles.
    private static IReadOnlyDictionary<string, string?> BuildProjectOwnerIndex(GraphSnapshot snapshot)
    {
        var nodes = snapshot.Nodes.ToDictionary(node => node.SymbolId, StringComparer.Ordinal);
        var owners = new Dictionary<string, string?>(StringComparer.Ordinal);
        var chain = new List<string>();

        foreach (var node in snapshot.Nodes)
        {
            if (owners.ContainsKey(node.SymbolId)) continue;
            var cursor = node;
            string? project;
            while (true)
            {
                if (owners.TryGetValue(cursor.SymbolId, out project))
                    break;
                if (cursor.Kind == GraphNodeKind.Project)
                {
                    project = cursor.Label;
                    owners[cursor.SymbolId] = project;
                    break;
                }

                chain.Add(cursor.SymbolId);
                if (cursor.ParentSymbolId is null)
                {
                    project = null;
                    break;
                }
                cursor = nodes[cursor.ParentSymbolId];
            }
            foreach (var id in chain)
                owners[id] = project;
            chain.Clear();
        }
        return owners;
    }
}
