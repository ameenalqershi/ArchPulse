// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortableHandoffContractTests
{
    private static readonly string Commit = new('a', 40);
    private static readonly string Digest = new('b', 64);

    private static PortableTaskEnvelope Sample() => new()
    {
        Schema = "archpulse.task-envelope/v1",
        TaskId = "T053",
        Goal = "Reconstruct task across agents",
        Repository = "https://github.com/ameenalqershi/ArchPulse",
        BaseCommit = Commit,
        WorkingBranch = "feat/t053-contracts",
        WorkspaceAlias = "agent-t053",
        RequirementIds = ["R-CONTINUITY-1"],
        AcceptanceCriteria = ["Independent parser can read this"],
        RequiredCapabilities = ["git.read", "tests.run"],
        AllowedTools = ["git.read"]
    };

    [Fact]
    public void PortableTaskRoundTripsWithExactCaseAndRevision()
    {
        var copy = PortableHandoffJson.ReadTask(PortableHandoffJson.Write(Sample()));
        Assert.Equal("T053", copy.TaskId);
        Assert.Equal(Commit, copy.BaseCommit);
        Assert.Equal(["git.read", "tests.run"], copy.RequiredCapabilities);
    }

    [Fact]
    public void CapabilityManifestIsAClaimNotAuthorization()
    {
        var manifest = new PortableCapabilityManifest
        {
            Schema = "archpulse.capability-manifest/v1",
            AgentId = "agent-b",
            Capabilities = ["git.read"]
        };
        Assert.Equal("agent-b",
            PortableHandoffJson.ReadCapabilities(PortableHandoffJson.Write(manifest)).AgentId);
    }

    [Fact]
    public void ApprovalRequiresCommitAndClaimEvidence()
    {
        var claim = new PortableApprovalReceipt
        {
            Schema = "archpulse.approval-receipt/v1",
            ApprovalId = "approval-1",
            TaskId = "T053",
            Commit = Commit,
            Decision = "Granted",
            AssertedBy = "unverified-human-name",
            EvidenceReference = "review-reference"
        };
        Assert.Equal("Granted", PortableHandoffJson.ReadApproval(PortableHandoffJson.Write(claim)).Decision);
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadApproval(
            PortableHandoffJson.Write(claim with { AssertedBy = null })));
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadApproval(
            PortableHandoffJson.Write(claim with { Commit = "main" })));
    }

    [Fact]
    public void UnknownExternalEffectNeverHasSuccessReceipt()
    {
        var effect = new PortableExternalEffectReceipt
        {
            Schema = "archpulse.external-effect-receipt/v1",
            TaskId = "T053",
            Action = "github.create-pr",
            IdempotencyKey = "create-pr-1",
            State = "Unknown",
            ReconciliationStep = "Inspect remote PR before retry"
        };
        Assert.Equal("Unknown", PortableHandoffJson.ReadEffect(PortableHandoffJson.Write(effect)).State);
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadEffect(
            PortableHandoffJson.Write(effect with { ReceiptSha256 = Digest })));
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadEffect(
            PortableHandoffJson.Write(effect with { ReconciliationStep = null })));
    }

    [Fact]
    public void SucceededExternalEffectNeedsDigestButIsNotAuthenticated()
    {
        var effect = new PortableExternalEffectReceipt
        {
            Schema = "archpulse.external-effect-receipt/v1",
            TaskId = "T053",
            Action = "github.create-pr",
            IdempotencyKey = "create-pr-1",
            State = "Succeeded"
        };
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadEffect(
            PortableHandoffJson.Write(effect)));
        Assert.Equal(Digest, PortableHandoffJson.ReadEffect(
            PortableHandoffJson.Write(effect with { ReceiptSha256 = Digest })).ReceiptSha256);
    }

    [Theory]
    [InlineData("archpulse.task-envelope/v0")]
    [InlineData("archpulse.task-envelope/v2")]
    [InlineData("archpulse.task-envelope/V1")]
    public void UnsupportedVersionsFailClosed(string value)
    {
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            PortableHandoffJson.Write(Sample() with { Schema = value })));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("-danger")]
    [InlineData("feature//invalid")]
    [InlineData("bad branch")]
    public void UnsafeBranchesFailClosed(string value)
    {
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            PortableHandoffJson.Write(Sample() with { WorkingBranch = value })));
    }

    [Fact]
    public void MissingUnexpectedAndDuplicateKeysFailClosed()
    {
        var json = PortableHandoffJson.Write(Sample());
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            json.Replace("\"taskId\":\"T053\",", "", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            json.Replace("\"taskId\":", "\"unexpectedField\":1,\"taskId\":", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            json.Replace("\"taskId\":", "\"taskId\":\"T999\",\"taskId\":", StringComparison.Ordinal)));
    }

    [Fact]
    public void DuplicateCapabilitiesAndFakeCommitsFailClosed()
    {
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            PortableHandoffJson.Write(Sample() with { RequiredCapabilities = ["git.read", "git.read"] })));
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            PortableHandoffJson.Write(Sample() with { BaseCommit = "main" })));
    }

    [Fact]
    public void OversizedPayloadAndPossibleCredentialFailWithoutLeakingText()
    {
        Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            PortableHandoffJson.Write(Sample() with { Goal = new string('x', 70_000) })));
        var error = Assert.Throws<InvalidDataException>(() => PortableHandoffJson.ReadTask(
            PortableHandoffJson.Write(Sample() with { Goal = "ghp_" + new string('Z', 35) })));
        Assert.DoesNotContain("ghp_", error.Message, StringComparison.Ordinal);
    }
}
