// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;

namespace ArchPulse.Guardian;

public enum ArchitectureVerdict { Pass, Violation, Unknown }
public enum ViolationSeverity { Info, Warning, Error, Blocker }

public sealed record ForbiddenDependencyRule(
    string Id, int Version, GraphEdgeKind EdgeKind,
    string FromSymbolPrefix, string ToSymbolPrefix,
    ViolationSeverity Severity, bool Enabled = true);

/// <summary>Scoped, human-approved and time-limited exception to one exact finding.</summary>
public sealed record ArchitectureWaiver(
    string RuleId, string EdgeId, string ApprovedBy, string Justification, DateTimeOffset ExpiresAt);

public sealed record ArchitectureViolation(
    string RuleId, int RuleVersion, string EdgeId,
    string SourceNodeId, string TargetNodeId, ViolationSeverity Severity,
    SourceSpan? Witness, bool Waived, string Explanation);

public sealed record ArchitectureEvaluation(
    ArchitectureVerdict Verdict,
    IReadOnlyList<ArchitectureViolation> Findings,
    IReadOnlyList<string> UnknownReasons)
{
    public bool BlocksCi => Findings.Any(x => !x.Waived && x.Severity is ViolationSeverity.Error or ViolationSeverity.Blocker);
}

public static class ArchitectureGuardian
{
    /// <summary>Deterministic rules over symbol-resolved edges; absence of analysis evidence is Unknown.</summary>
    public static ArchitectureEvaluation Evaluate(
        GraphSnapshot snapshot, IEnumerable<ForbiddenDependencyRule> rules,
        IEnumerable<ArchitectureWaiver>? waivers = null,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        var integrity = snapshot.VerifyIntegrity();
        var unknown = snapshot.Diagnostics.Concat(integrity).ToList();
        var now = evaluatedAtUtc ?? DateTimeOffset.UtcNow;
        var activeWaivers = (waivers ?? []).Where(w => w.ExpiresAt > now &&
            !string.IsNullOrWhiteSpace(w.ApprovedBy) &&
            !string.IsNullOrWhiteSpace(w.Justification)).ToArray();
        var nodes = snapshot.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var findings = new List<ArchitectureViolation>();
        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if (string.IsNullOrWhiteSpace(rule.Id) || rule.Version < 1 ||
                string.IsNullOrWhiteSpace(rule.FromSymbolPrefix) ||
                string.IsNullOrWhiteSpace(rule.ToSymbolPrefix))
            {
                unknown.Add("Invalid policy with missing ID, version or selectors.");
                continue;
            }
            foreach (var edge in snapshot.Edges.Where(e => e.Kind == rule.EdgeKind))
            {
                if (!nodes.TryGetValue(edge.From, out var from) ||
                    !nodes.TryGetValue(edge.To, out var to))
                {
                    unknown.Add($"Cannot evaluate dangling edge {edge.Id}");
                    continue;
                }
                var fromKey = from.Evidence.SymbolKey ?? string.Empty;
                var toKey = to.Evidence.SymbolKey ?? string.Empty;
                if (!fromKey.StartsWith(rule.FromSymbolPrefix, StringComparison.Ordinal) ||
                    !toKey.StartsWith(rule.ToSymbolPrefix, StringComparison.Ordinal))
                    continue;
                var waived = activeWaivers.Any(w => w.RuleId == rule.Id && w.EdgeId == edge.Id);
                findings.Add(new ArchitectureViolation(
                    rule.Id, rule.Version, edge.Id, edge.From, edge.To, rule.Severity,
                    edge.Evidence.Source, waived,
                    $"{from.Name} {rule.EdgeKind} {to.Name} violates {rule.Id} v{rule.Version}"));
            }
        }
        var verdict = findings.Any(f => !f.Waived) ? ArchitectureVerdict.Violation :
            unknown.Count > 0 ? ArchitectureVerdict.Unknown : ArchitectureVerdict.Pass;
        return new ArchitectureEvaluation(verdict, findings, unknown);
    }
}
