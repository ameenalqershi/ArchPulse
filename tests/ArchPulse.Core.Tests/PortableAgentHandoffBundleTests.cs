// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortableAgentHandoffBundleTests
{
    private static readonly string Commit = new('a', 40);
    private static readonly string Head = new('b', 40);

    [Fact]
    public void TwoIndependentConsumersRecoverSamePortableWorkWithoutConversation()
    {
        using var f = new Fixture();
        var message = f.MakeBundle();
        var first = PortableAgentHandoffJson.Read(f.Root, message);
        var second = PortableAgentHandoffJson.Read(f.Root, message);
        Assert.Equal(first, second, new BundleComparer());
        Assert.Equal("T057", second.TaskId);
        Assert.Equal(["Review tests", "Confirm remote CI"], second.NextSteps);
    }

    [Fact]
    public void ReplacedUnknownEffectsRemainVisibleAndStatusIsNotVerified()
    {
        using var f = new Fixture(withUnknown: true);
        var bundle = PortableAgentHandoffJson.Read(f.Root, f.MakeBundle());
        Assert.Equal("RecoveryRequired", bundle.Status);
        Assert.Equal(["effect-a"], bundle.UnknownEffectIds);
    }

    [Fact]
    public void StaleHandoffRejectedAfterNewCheckpoint()
    {
        using var f = new Fixture();
        var json = f.MakeBundle();
        var ledger = new PortableTaskLedger(f.Root);
        var last = ledger.Read("T057")[^1];
        ledger.Append("T057", last.Checkpoint.Sequence, last.Digest,
            last.Checkpoint.EnvelopeSha256, "Checkpointed",
            last.Checkpoint.EvidenceSha256, []);
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Read(f.Root, json));
    }

    [Fact]
    public void DeletedEvidencePreventsReceiverAcceptance()
    {
        using var f = new Fixture();
        var json = f.MakeBundle();
        File.Delete(Path.Combine(f.Root, "T057", "cas", f.Artifact!.Sha256 + ".txt"));
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Read(f.Root, json));
    }

    [Fact]
    public void DuplicateKeysRejectedEvenWhenLatestValueIsValid()
    {
        using var f = new Fixture();
        var json = f.MakeBundle();
        var duplicate = json.Replace("\"taskId\":\"T057\"",
            "\"taskId\":\"T999\",\"taskId\":\"T057\"", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Read(f.Root, duplicate));
    }

    [Fact]
    public void TamperedHandoffDigestOrNextStepsFailClosed()
    {
        using var f = new Fixture();
        var json = f.MakeBundle();
        var forged = json.Replace("\"checkpointDigest\":\"" + f.FinalDigest + "\"",
            "\"checkpointDigest\":\"" + new string('f', 64) + "\"", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Read(f.Root, forged));
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Read(f.Root,
            json.Replace("\"nextSteps\":[", "\"nextSteps\":[\"ghp_" + new string('A', 25) + "\",",
                StringComparison.Ordinal)));
    }

    [Fact]
    public void IdentityAndEnvelopeMismatchRejected()
    {
        using var f = new Fixture();
        var json = f.MakeBundle();
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Read(f.Root,
            json.Replace("\"taskId\":\"T057\"", "\"taskId\":\"T058\"",
                StringComparison.Ordinal)));
    }

    [Fact]
    public void MissingManifestAnchorCannotBeExported()
    {
        using var f = new Fixture();
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Create(f.Root,
            "T057", f.Envelope, new string('d', 64), ["Next"]));
    }

    [Fact]
    public void UnsafeNextStepsRefused()
    {
        using var f = new Fixture();
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Create(f.Root,
            "T057", f.Envelope, f.ManifestHash!, ["ghp_" + new string('a', 30)]));
        Assert.Throws<InvalidDataException>(() => PortableAgentHandoffJson.Create(f.Root,
            "T057", f.Envelope, f.ManifestHash!, []));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-t057-" + Guid.NewGuid().ToString("N"));
        public string Envelope { get; }
        public string? ManifestHash { get; }
        public PortableArtifactReference? Artifact { get; }
        public string FinalDigest { get; }
        public Fixture(bool withUnknown = false)
        {
            var envelope = new PortableTaskEnvelope
            {
                Schema = "archpulse.task-envelope/v1",
                TaskId = "T057",
                Goal = "Resume verified source-backed work",
                Repository = "https://github.com/ameenalqershi/ArchPulse",
                BaseCommit = Commit,
                WorkingBranch = "feat/recovery",
                WorkspaceAlias = "safe-workspace",
                RequirementIds = ["R-CONTINUITY"],
                AcceptanceCriteria = ["Verify exact source evidence"],
                RequiredCapabilities = ["git"],
                AllowedTools = ["git"]
            };
            Envelope = PortableHandoffJson.Write(envelope);
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Envelope)));
            var ledger = new PortableTaskLedger(Root);
            var first = ledger.Append("T057", 0, PortableTaskLedger.GenesisDigest, sha,
                "Planned", [], withUnknown ? ["effect-a"] : []);
            var store = new PortableArtifactEvidenceStore(Root);
            Artifact = store.PutArtifact("T057", "tests", "test-report", Head, "Passed: 1");
            ManifestHash = store.PutManifest("T057", Commit, Head, [Artifact]).Sha256;
            var final = ledger.Append("T057", 1, first.Digest, sha,
                withUnknown ? "RecoveryRequired" : "Checkpointed",
                [ManifestHash], withUnknown ? ["effect-a"] : []);
            FinalDigest = final.Digest;
        }
        public string MakeBundle() => PortableAgentHandoffJson.Create(Root, "T057",
            Envelope, ManifestHash!, ["Review tests", "Confirm remote CI"]);
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class BundleComparer : IEqualityComparer<PortableAgentHandoffBundle>
    {
        public bool Equals(PortableAgentHandoffBundle? x, PortableAgentHandoffBundle? y) =>
            x?.TaskId == y?.TaskId && x?.CheckpointDigest == y?.CheckpointDigest &&
            x?.TaskEnvelopeJson == y?.TaskEnvelopeJson &&
            (x?.NextSteps ?? []).SequenceEqual(y?.NextSteps ?? []);
        public int GetHashCode(PortableAgentHandoffBundle obj) => HashCode.Combine(obj.TaskId, obj.CheckpointDigest);
    }
}
