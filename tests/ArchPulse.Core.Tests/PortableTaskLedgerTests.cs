// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortableTaskLedgerTests
{
    private static readonly string Envelope = new('a', 64);
    private static readonly string Evidence = new('b', 64);

    [Fact]
    public void ReopensTwoCommittedCheckpointsWithDigestLinkedHistory()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        var first = store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Planned", [], []);
        var second = store.Append("T054", 1, first.Digest,
            Envelope, "Checkpointed", [Evidence], []);
        var recovered = new PortableTaskLedger(temp.Path).Read("T054");
        Assert.Equal(2, recovered.Count);
        Assert.Equal(PortableTaskLedger.GenesisDigest, first.Checkpoint.PreviousDigest);
        Assert.Equal(first.Digest, second.Checkpoint.PreviousDigest);
        Assert.Equal(second.Digest, recovered[^1].Digest);
        Assert.Equal([Evidence], recovered[^1].Checkpoint.EvidenceSha256);
    }

    [Fact]
    public void StaleRevisionCannotOverwriteNewerCommittedCheckpoint()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        var first = store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Planned", [], []);
        Assert.Throws<InvalidOperationException>(() => store.Append("T054", 0,
            PortableTaskLedger.GenesisDigest, Envelope, "Checkpointed", [], []));
        Assert.Throws<InvalidOperationException>(() => store.Append("T054", 1,
            PortableTaskLedger.GenesisDigest, Envelope, "Checkpointed", [], []));
        Assert.Equal(first.Digest, store.Read("T054").Single().Digest);
    }

    [Fact]
    public void TamperedCommittedCheckpointFailsClosed()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Planned", [], []);
        var filename = temp.Entry(1);
        File.WriteAllText(filename, File.ReadAllText(filename)
            .Replace("\"status\":\"Planned\"", "\"status\":\"HandoffReady\"",
                StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => store.Read("T054"));
        Assert.Throws<InvalidDataException>(() => store.Append("T054", 1,
            new string('0', 64), Envelope, "Checkpointed", [], []));
    }

    [Fact]
    public void DuplicateNestedKeyRejectedBeforeLastValueWinsDeserialization()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Planned", [], []);
        var file = temp.Entry(1);
        var original = File.ReadAllText(file);
        Assert.Contains("\"taskId\":\"T054\"", original, StringComparison.Ordinal);
        File.WriteAllText(file, original.Replace(
            "\"taskId\":\"T054\"", "\"taskId\":\"T999\",\"taskId\":\"T054\"",
            StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => store.Read("T054"));
    }

    [Fact]
    public void GapInCommittedSequenceIsNotInterpretedAsEmptyHistory()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        var first = store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Planned", [], []);
        store.Append("T054", 1, first.Digest, Envelope, "Checkpointed", [], []);
        File.Delete(temp.Entry(1));
        Assert.Throws<InvalidDataException>(() => store.Read("T054"));
    }

    [Fact]
    public void CrashOrphanPendingFileNeverCountsAsCommitted()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        var first = store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Checkpointed", [], []);
        File.WriteAllText(Path.Combine(temp.Path, "T054", ".orphan.pending"), "{partial");
        Assert.Single(new PortableTaskLedger(temp.Path).Read("T054"));
        var second = store.Append("T054", 1, first.Digest, Envelope, "RecoveryRequired", [], ["effect-1"]);
        Assert.Equal("RecoveryRequired", second.Checkpoint.Status);
        Assert.Equal(2, store.Read("T054").Count);
    }

    [Fact]
    public void UnreconciledEffectCannotMoveToHandoffReady()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        Assert.Throws<InvalidDataException>(() => store.Append("T054", 0,
            PortableTaskLedger.GenesisDigest, Envelope, "HandoffReady", [],
            ["github-create-pr"]));
        Assert.Empty(store.Read("T054"));
        var recovery = store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "RecoveryRequired", [], ["github-create-pr"]);
        Assert.Throws<InvalidOperationException>(() => store.Append("T054", 1,
            recovery.Digest, Envelope, "HandoffReady", [Evidence], []));
        var checkpoint = store.Append("T054", 1, recovery.Digest,
            Envelope, "Checkpointed", [Evidence], ["github-create-pr"]);
        Assert.Equal("Checkpointed", checkpoint.Checkpoint.Status);
        Assert.Equal(["github-create-pr"], store.Read("T054")[^1].Checkpoint.UnknownEffectIds);
    }

    [Fact]
    public void ChangingEnvelopeWithoutMigrationFailsClosed()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        var first = store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Planned", [], []);
        Assert.Throws<InvalidOperationException>(() => store.Append("T054", 1,
            first.Digest, new string('f', 64), "Checkpointed", [], []));
        Assert.Single(store.Read("T054"));
    }

    [Fact]
    public void UnsupportedStatesAndUnboundedListsRejected()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        Assert.Throws<InvalidDataException>(() => store.Append("T054", 0,
            PortableTaskLedger.GenesisDigest, Envelope, "CompletedVerified", [], []));
        Assert.Throws<InvalidDataException>(() => store.Append("T054", 0,
            PortableTaskLedger.GenesisDigest, Envelope, "Planned",
            Enumerable.Repeat(Evidence, 33).ToArray(), []));
        Assert.Throws<InvalidDataException>(() => store.Append("T054", 0,
            PortableTaskLedger.GenesisDigest, Envelope, "Planned", [Evidence, Evidence], []));
        Assert.Empty(store.Read("T054"));
    }

    [Fact]
    public void UntrustedTaskIdCannotEscapeRoot()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        Assert.Throws<ArgumentException>(() => store.Read("../T054"));
        Assert.Throws<ArgumentException>(() => store.Append("../T054", 0,
            PortableTaskLedger.GenesisDigest, Envelope, "Planned", [], []));
    }

    [Fact]
    public async Task TwoCompetingAgentsHaveOnlyOneCommittedFirstCheckpoint()
    {
        using var temp = new TempLedger();
        var firstAgent = new PortableTaskLedger(temp.Path);
        var secondAgent = new PortableTaskLedger(temp.Path);
        using var start = new ManualResetEventSlim(false);
        Task<bool> Attempt(PortableTaskLedger agent) => Task.Run(() =>
        {
            start.Wait();
            try
            {
                agent.Append("T054", 0, PortableTaskLedger.GenesisDigest,
                    Envelope, "Planned", [], []);
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                return false;
            }
        });
        var a = Attempt(firstAgent);
        var b = Attempt(secondAgent);
        start.Set();
        var outcomes = await Task.WhenAll(a, b);
        Assert.Single(outcomes, success => success);
        Assert.Single(firstAgent.Read("T054"));
    }

    [Fact]
    public void LinkedEventCannotMasqueradeAsCommittedCheckpoint()
    {
        using var temp = new TempLedger();
        var store = new PortableTaskLedger(temp.Path);
        var first = store.Append("T054", 0, PortableTaskLedger.GenesisDigest,
            Envelope, "Planned", [], []);
        var second = temp.Entry(2);
        File.CreateSymbolicLink(second, temp.Entry(1));
        Assert.Throws<InvalidDataException>(() => store.Read("T054"));
        Assert.Equal(64, first.Digest.Length);
    }

    private sealed class TempLedger : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "archpulse-t054-" + Guid.NewGuid().ToString("N"));
        public string Entry(int sequence) => System.IO.Path.Combine(Path, "T054", $"{sequence:D8}.json");
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
