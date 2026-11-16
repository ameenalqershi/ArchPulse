// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Core;

// S11/T058 follow-up: caller-supplied GitHub PR snapshots are untrusted observations.
// Nothing here queries GitHub, grants permission, or authorizes a retry.
public sealed record PortablePullRequestCandidate
{
    public required int Number { get; init; }
    public required string Repository { get; init; }
    public required string HeadBranch { get; init; }
    public required string BaseBranch { get; init; }
    public required string HeadCommit { get; init; }
}

public sealed record PortablePullRequestSnapshot
{
    public required bool QueryComplete { get; init; }
    public required PortablePullRequestCandidate[] Candidates { get; init; }
}

public static class PortablePullRequestRecovery
{
    public static PortableEffectRecoveryResult Evaluate(
        PortableExternalEffectReceipt local, string repository, string headBranch,
        string baseBranch, string expectedHeadCommit, PortablePullRequestSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(local);
        _ = PortableHandoffJson.ReadEffect(PortableHandoffJson.Write(local));
        if (local.Action != "github.create-pr")
            throw new ArgumentException("Only github.create-pr effects are supported.", nameof(local));
        if (!IsRepository(repository) || !IsBranch(headBranch) || !IsBranch(baseBranch) ||
            !IsSha(expectedHeadCommit))
            throw new ArgumentException("Invalid expected repository, branch, or head commit.");

        if (snapshot is null || !snapshot.QueryComplete || snapshot.Candidates is null ||
            snapshot.Candidates.Length > 256)
            return Unknown("Remote PR listing is absent, incomplete or oversized.");

        var seen = new HashSet<int>();
        foreach (var candidate in snapshot.Candidates)
        {
            if (candidate is null || candidate.Number <= 0 || !seen.Add(candidate.Number) ||
                !IsRepository(candidate.Repository) || !IsBranch(candidate.HeadBranch) ||
                !IsBranch(candidate.BaseBranch) || !IsSha(candidate.HeadCommit))
                return Diverged("Remote PR listing contains invalid or ambiguous identities.");
        }

        var matches = snapshot.Candidates.Where(c =>
            c.Repository == repository && c.HeadBranch == headBranch &&
            c.BaseBranch == baseBranch).ToArray();
        if (matches.Length == 0)
            return Unknown("No matching PR was observed; absence does not authorize retry.");
        if (matches.Length > 1)
            return Diverged("Multiple PRs share the expected repository and branch identity.");
        if (matches[0].HeadCommit != expectedHeadCommit)
            return Diverged("PR branch exists but the observed head commit differs.");
        if (local.State == "Failed")
            return Diverged("PR exists even though the local operation claimed failure.");

        // An exact matching PR is only observed, not independently authenticated or approved.
        return new PortableEffectRecoveryResult(EffectRecoveryDecision.Confirmed, false,
            "One matching PR was observed; verify API provenance and never repeat create.");
    }

    private static bool IsSha(string? value) =>
        value is { Length: 40 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsRepository(string? value)
    {
        if (value is null || !value.StartsWith("https://github.com/", StringComparison.Ordinal) ||
            value.Length > 180 || value.Contains('?') ||
            value.Contains('#')) return false;
        var parts = value["https://github.com/".Length..].Split('/');
        return parts.Length == 2 && parts.All(IsSegment);
    }

    private static bool IsSegment(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 80 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static bool IsBranch(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        !value.StartsWith('-') && !value.StartsWith('/') &&
        !value.Contains("..", StringComparison.Ordinal) &&
        !value.Contains("//", StringComparison.Ordinal) &&
        !value.Contains('\\') && value.All(c =>
            char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.');

    private static PortableEffectRecoveryResult Unknown(string reason) =>
        new(EffectRecoveryDecision.Unknown, false, reason);
    private static PortableEffectRecoveryResult Diverged(string reason) =>
        new(EffectRecoveryDecision.Diverged, false, reason);
}
