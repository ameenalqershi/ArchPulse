// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortableExternalEffectReconciliationTests
{
    private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static PortableExternalEffectReceipt Local(string state = "Unknown") => new()
    {
        Schema = "archpulse.external-effect-receipt/v1",
        TaskId = "T058",
        Action = "github.create-pr",
        IdempotencyKey = "pr-create-1",
        State = state,
        ReceiptSha256 = state == "Succeeded" ? Digest : null,
        ReconciliationStep = state == "Unknown" ? "Check all matching remote PRs before retry" : null
    };
    private static PortableRemoteEffectObservation Remote(string state = "Found") => new()
    {
        TaskId = "T058",
        Action = "github.create-pr",
        IdempotencyKey = "pr-create-1",
        State = state,
        ReceiptSha256 = state == "Found" ? Digest : null
    };

    [Theory]
    [InlineData("Unknown", null, EffectRecoveryDecision.Unknown)]
    [InlineData("Unknown", "NotFound", EffectRecoveryDecision.Unknown)]
    [InlineData("Unknown", "Inconclusive", EffectRecoveryDecision.Unknown)]
    [InlineData("Unknown", "Found", EffectRecoveryDecision.Confirmed)]
    [InlineData("Succeeded", "Found", EffectRecoveryDecision.Confirmed)]
    [InlineData("Failed", "Found", EffectRecoveryDecision.Diverged)]
    public void NeverIssuesAutomaticRetry(string state, string? remote, EffectRecoveryDecision expected)
    {
        var result = PortableExternalEffectReconciliation.Evaluate(Local(state),
            remote is null ? null : Remote(remote));
        Assert.Equal(expected, result.Decision);
        Assert.False(result.SafeToRetry);
    }

    [Fact]
    public void MismatchedIdentityMustNotConfirmUnrelatedPullRequest()
    {
        var result = PortableExternalEffectReconciliation.Evaluate(Local(),
            Remote() with { IdempotencyKey = "other-pr" });
        Assert.Equal(EffectRecoveryDecision.Diverged, result.Decision);
    }

    [Fact]
    public void FailedOrForgedRemoteReceiptNeverConfirms()
    {
        var malformed = PortableExternalEffectReconciliation.Evaluate(Local(),
            Remote() with { ReceiptSha256 = "bad" });
        Assert.Equal(EffectRecoveryDecision.Unknown, malformed.Decision);
        var mismatch = PortableExternalEffectReconciliation.Evaluate(Local("Succeeded"),
            Remote() with { ReceiptSha256 = new string('b', 64) });
        Assert.Equal(EffectRecoveryDecision.Diverged, mismatch.Decision);
    }

    [Fact]
    public void InvalidLocalEffectFailsClosed()
    {
        Assert.Throws<InvalidDataException>(() => PortableExternalEffectReconciliation.Evaluate(
            Local("Unknown") with { ReconciliationStep = null }, Remote()));
    }
}
