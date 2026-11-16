// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortablePullRequestRecoveryTests
{
    private const string Repo = "https://github.com/ameenalqershi/ArchPulse";
    private const string Head = "feat/recovery-pr";
    private const string Base = "main";
    private static readonly string Commit = new('a', 40);
    private static PortableExternalEffectReceipt Receipt(string state = "Unknown") => new()
    {
        Schema = "archpulse.external-effect-receipt/v1",
        TaskId = "T058",
        Action = "github.create-pr",
        IdempotencyKey = "pr-create-01",
        State = state,
        ReceiptSha256 = state == "Succeeded" ? new string('b', 64) : null,
        ReconciliationStep = state == "Unknown" ? "Inspect PRs before retry" : null
    };
    private static PortablePullRequestCandidate Candidate(int number = 1) => new()
    {
        Number = number, Repository = Repo, HeadBranch = Head, BaseBranch = Base,
        HeadCommit = Commit
    };
    private static PortablePullRequestSnapshot Snapshot(params PortablePullRequestCandidate[] items) =>
        new() { QueryComplete = true, Candidates = items };
    private static PortableEffectRecoveryResult Check(
        PortablePullRequestSnapshot? snapshot, string state = "Unknown") =>
        PortablePullRequestRecovery.Evaluate(Receipt(state), Repo, Head, Base, Commit, snapshot);

    [Fact]
    public void OneMatchingRemotePrIsObservedButNeverRetried()
    {
        var result = Check(Snapshot(Candidate()));
        Assert.Equal(EffectRecoveryDecision.Confirmed, result.Decision);
        Assert.False(result.SafeToRetry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrIncompleteListingCannotAuthorizeRetry(bool incomplete)
    {
        var result = Check(incomplete
            ? new PortablePullRequestSnapshot { QueryComplete = false, Candidates = [Candidate()] }
            : null);
        Assert.Equal(EffectRecoveryDecision.Unknown, result.Decision);
        Assert.False(result.SafeToRetry);
    }

    [Fact]
    public void NoMatchingCandidateNeverMeansSafeToCreate()
    {
        var result = Check(Snapshot(Candidate() with { HeadBranch = "unrelated" }));
        Assert.Equal(EffectRecoveryDecision.Unknown, result.Decision);
        Assert.False(result.SafeToRetry);
    }

    [Fact]
    public void TwoMatchingPullRequestsAreDivergedRatherThanArbitrarilyChosen()
    {
        var result = Check(Snapshot(Candidate(1), Candidate(2)));
        Assert.Equal(EffectRecoveryDecision.Diverged, result.Decision);
        Assert.False(result.SafeToRetry);
    }

    [Fact]
    public void RepeatedRemoteNumberIsRejectedAsAmbiguous()
    {
        Assert.Equal(EffectRecoveryDecision.Diverged,
            Check(Snapshot(Candidate(1), Candidate(1))).Decision);
    }

    [Fact]
    public void ChangedHeadCommitNeverConfirmsStalePr()
    {
        var result = Check(Snapshot(Candidate() with { HeadCommit = new string('c', 40) }));
        Assert.Equal(EffectRecoveryDecision.Diverged, result.Decision);
    }

    [Fact]
    public void ClaimedLocalFailureConflictsWithExistingRemotePr()
    {
        var result = Check(Snapshot(Candidate()), "Failed");
        Assert.Equal(EffectRecoveryDecision.Diverged, result.Decision);
        Assert.False(result.SafeToRetry);
    }

    [Fact]
    public void InvalidRemoteRepositoryOrInvalidReferenceFailsClosed()
    {
        Assert.Equal(EffectRecoveryDecision.Diverged,
            Check(Snapshot(Candidate() with { Repository = "https://evil.example/other" })).Decision);
        Assert.Throws<ArgumentException>(() =>
            PortablePullRequestRecovery.Evaluate(Receipt(), Repo, "../bad", Base, Commit,
                Snapshot(Candidate())));
    }

    [Fact]
    public void OtherActionIsExplicitlyUnsupported()
    {
        Assert.Throws<ArgumentException>(() =>
            PortablePullRequestRecovery.Evaluate(Receipt() with { Action = "github.comment" },
                Repo, Head, Base, Commit, Snapshot(Candidate())));
    }

    [Fact]
    public void OversizedSnapshotIsUnknownNotConfirmation()
    {
        var entries = Enumerable.Range(1, 257).Select(Candidate).ToArray();
        Assert.Equal(EffectRecoveryDecision.Unknown, Check(Snapshot(entries)).Decision);
    }
}
