// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using Xunit;
namespace ArchPulse.Core.Tests;
public sealed class EvidenceReceiptTests
{
    [Fact]
    public void VerifiedClaimRequiresEvidenceSourceAndRevision()
    {
        Assert.Throws<ArgumentException>(() => EvidenceReceipt.Create(EvidenceKind.Verified, null, "sha"));
        Assert.Throws<ArgumentException>(() => EvidenceReceipt.Create(EvidenceKind.Verified, "trace", ""));
    }

    [Fact]
    public void ProposedClaimIsNotAutomaticallyVerified()
    {
        var receipt = EvidenceReceipt.Create(EvidenceKind.Proposed, null, null);
        Assert.Equal(EvidenceKind.Proposed, receipt.Kind);
    }

    [Fact]
    public void VerifiedReceiptHasEvidenceReferencesButIsNotProofOfExternalValidation()
    {
        var receipt = EvidenceReceipt.Create(EvidenceKind.Verified, "run-artifact", "sha256:example");
        Assert.Equal("run-artifact", receipt.Source);
        Assert.Equal("sha256:example", receipt.Revision);
    }

    [Fact]
    public void InvalidEvidenceKindIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EvidenceReceipt.Create((EvidenceKind)999, "source", "rev"));
    }
}
