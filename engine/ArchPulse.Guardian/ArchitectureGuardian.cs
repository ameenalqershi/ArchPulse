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
        var projectIds = snapshot.Nodes.Where(n => n.Kind == GraphNodeKind.Project)
            .Select(n => (Id: n.SymbolId, Name: n.Label)).ToArray();
        var projectNames = projectIds.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if (string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.Version)
                || string.IsNullOrWhiteSpace(rule.FromProject) || string.IsNullOrWhiteSpace(rule.ToProject)
                || string.IsNullOrWhiteSpace(rule.Relation))
                throw new ArgumentException("Policy ID, version, project names and relation are required.");
            if (!projectNames.Contains(rule.FromProject) || !projectNames.Contains(rule.ToProject))
            {
                result.Add(new ArchitectureFinding(rule.Id, rule.Version, rule.Severity,
                    RuleOutcome.NotEvaluated, null, null, null, null,
                    "One or both projects are absent from this revision; rule cannot be evaluated."));
                continue;
            }

            var matches = snapshot.Edges.Where(edge =>
                edge.Relation == rule.Relation
                && IsOwnedBy(edge.FromSymbolId, rule.FromProject)
                && IsOwnedBy(edge.ToSymbolId, rule.ToProject)).ToArray();

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

    private static bool IsOwnedBy(string symbolId, string projectName) =>
        symbolId == $"project:{projectName}" ||
        symbolId.StartsWith(projectName + "::", StringComparison.Ordinal);
}
