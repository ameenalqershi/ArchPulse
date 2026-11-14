// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortableTaskLeaseStoreTests
{
    private static readonly string Envelope = new('a', 64);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    [Fact]
    public void ClaimAndReadAcrossInstancesUsesOpaqueSecretAndEpochOne()
    {
        using var fixture = new Fixture();
        var a = fixture.Store();
        var grant = a.Claim("T055", "agent-a", Duration);
        var b = fixture.Store();
        Assert.Equal(1L, grant.Epoch);
        Assert.True(b.IsCurrent(grant));
        Assert.Single(b.ReadEvents("T055"));
        Assert.DoesNotContain(grant.SecretToken,
            File.ReadAllText(fixture.Entry(1)), StringComparison.Ordinal);
        Assert.Equal("Claim", b.ReadEvents("T055")[0].Event.Operation);
    }

    [Fact]
    public void ClaimCannotRunWithoutCanonicalTaskCheckpoint()
    {
        using var fixture = new Fixture(withTask: false);
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Store().Claim("T055", "agent-a", Duration));
        Assert.Empty(fixture.Store().ReadEvents("T055"));
    }

    [Fact]
    public void StaleWorkerRejectedAfterExpiryAndReassignment()
    {
        using var fixture = new Fixture();
        var a = fixture.Store();
        var old = a.Claim("T055", "agent-a", Duration);
        fixture.Clock.Advance(Duration);
        Assert.False(a.IsCurrent(old));
        var newer = fixture.Store().Claim("T055", "agent-b", Duration);
        Assert.Equal(old.Epoch + 1, newer.Epoch);
        Assert.False(a.IsCurrent(old));
        Assert.True(a.IsCurrent(newer));
        Assert.Throws<InvalidOperationException>(() => a.Renew(old, Duration));
        Assert.Throws<InvalidOperationException>(() => a.Release(old));
        Assert.Equal(2, a.ReadEvents("T055").Count);
    }

    [Fact]
    public void RenewalRetainsEpochAndRotatingAgentCannotForgeOldToken()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var first = store.Claim("T055", "agent-a", Duration);
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        var renewed = store.Renew(first, Duration);
        Assert.Equal(first.Epoch, renewed.Epoch);
        Assert.Equal(first.SecretToken, renewed.SecretToken);
        Assert.True(renewed.ExpiresUtc > first.ExpiresUtc);
        Assert.True(store.IsCurrent(renewed));
        Assert.False(store.IsCurrent(renewed with { SecretToken = new string('0', 64) }));
        Assert.False(store.IsCurrent(renewed with { Epoch = first.Epoch + 1 }));
        Assert.Throws<InvalidOperationException>(() =>
            store.Renew(renewed with { AgentId = "agent-b" }, Duration));
        Assert.Equal(["Claim", "Renew"],
            store.ReadEvents("T055").Select(x => x.Event.Operation).ToArray());
    }

    [Fact]
    public void ReleaseAllowsNewOwnerWithoutWaitingTtl()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var a = store.Claim("T055", "agent-a", Duration);
        store.Release(a);
        Assert.False(store.IsCurrent(a));
        var b = fixture.Store().Claim("T055", "agent-b", Duration);
        Assert.Equal(2L, b.Epoch);
        Assert.False(store.IsCurrent(a));
        Assert.True(store.IsCurrent(b));
        Assert.Equal(3, store.ReadEvents("T055").Count);
    }

    [Fact]
    public async Task TwoConcurrentClaimantsCannotBothOwnSameEpoch()
    {
        using var fixture = new Fixture();
        using var start = new ManualResetEventSlim();
        Task<PortableTaskLeaseGrant?> Attempt(string agent) => Task.Run(() =>
        {
            start.Wait();
            try { return fixture.Store().Claim("T055", agent, Duration); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                return null;
            }
        });
        var a = Attempt("agent-a");
        var b = Attempt("agent-b");
        start.Set();
        var grants = await Task.WhenAll(a, b);
        var winner = Assert.Single(grants, g => g is not null);
        Assert.True(fixture.Store().IsCurrent(winner!));
        Assert.Single(fixture.Store().ReadEvents("T055"));
    }

    [Fact]
    public void ExpiryThenAgentReturnsCannotOverrideCurrentOwner()
    {
        using var fixture = new Fixture();
        var old = fixture.Store().Claim("T055", "agent-a", Duration);
        fixture.Clock.Advance(Duration.Add(TimeSpan.FromSeconds(1)));
        var latest = fixture.Store().Claim("T055", "agent-b", Duration);
        Assert.Throws<InvalidOperationException>(() => fixture.Store().Release(old));
        Assert.Throws<InvalidOperationException>(() => fixture.Store().Renew(old, Duration));
        Assert.True(fixture.Store().IsCurrent(latest));
        Assert.Equal(latest.Epoch, fixture.Store().ReadEvents("T055")[^1].Event.Epoch);
    }

    [Fact]
    public void ReplayedTamperedEventCannotBeAccepted()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        store.Claim("T055", "agent-a", Duration);
        var path = fixture.Entry(1);
        File.WriteAllText(path, File.ReadAllText(path)
            .Replace("\"agentId\":\"agent-a\"", "\"agentId\":\"agent-z\"",
                StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => store.ReadEvents("T055"));
        Assert.Throws<InvalidDataException>(() => store.Claim("T055", "agent-b", Duration));
    }

    [Fact]
    public void DuplicatePropertyAndMissingEntryAreFatal()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var first = store.Claim("T055", "agent-a", Duration);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        store.Renew(first, Duration);
        var path = fixture.Entry(1);
        var json = File.ReadAllText(path);
        File.WriteAllText(path, json.Replace("\"epoch\":1,", "\"epoch\":2,\"epoch\":1,",
            StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => store.ReadEvents("T055"));
        File.WriteAllText(path, json);
        File.Delete(path);
        Assert.Throws<InvalidDataException>(() => store.ReadEvents("T055"));
    }

    [Fact]
    public void OrphanPendingFileDoesNotBecomeCommittedEvent()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var first = store.Claim("T055", "agent-a", Duration);
        File.WriteAllText(Path.Combine(fixture.LeasesPath, ".crashed.pending"), "{partial");
        Assert.Single(fixture.Store().ReadEvents("T055"));
        store.Release(first);
        Assert.Equal(2, store.ReadEvents("T055").Count);
    }

    [Fact]
    public void InvalidTtlAndTaskIdRejectedBeforeLease()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            store.Claim("T055", "agent-a", TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            store.Claim("T055", "agent-a", TimeSpan.FromHours(12)));
        Assert.Throws<ArgumentException>(() =>
            store.Claim("../escape", "agent-a", Duration));
        Assert.Throws<InvalidDataException>(() =>
            store.Claim("T055", "../agent-a", Duration));
        Assert.Empty(store.ReadEvents("T055"));
    }

    [Fact]
    public void ClockRegressionFailsClosed()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var initial = store.Claim("T055", "agent-a", Duration);
        fixture.Clock.Advance(TimeSpan.FromSeconds(-1));
        Assert.Throws<InvalidDataException>(() => store.IsCurrent(initial));
        Assert.Throws<InvalidDataException>(() => store.Claim("T055", "agent-b", Duration));
    }

    [Fact]
    public void TokenCannotBeReusedWithAnotherTaskOrIdentity()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var original = store.Claim("T055", "agent-a", Duration);
        Assert.False(store.IsCurrent(original with { AgentId = "agent-b" }));
        Assert.False(store.IsCurrent(original with { SecretToken = "secret" }));
        Assert.Throws<InvalidOperationException>(() => store.Renew(
            original with { SecretToken = "secret" }, Duration));
    }

    [Fact]
    public void SymlinkedCommittedEventMustNotBeRead()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        store.Claim("T055", "agent-a", Duration);
        var first = fixture.Entry(1);
        var second = fixture.Entry(2);
        File.CreateSymbolicLink(second, first);
        Assert.Throws<InvalidDataException>(() => store.ReadEvents("T055"));
    }

    [Fact]
    public void FencedCheckpointRejectsOldAgentAfterTakeover()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var old = store.Claim("T055", "agent-a", Duration);
        var initial = new PortableTaskLedger(fixture.Root).Read("T055")[^1];
        fixture.Clock.Advance(Duration);
        var current = store.Claim("T055", "agent-b", Duration);
        Assert.Throws<InvalidOperationException>(() => store.AppendCheckpoint(old,
            initial.Checkpoint.Sequence, initial.Digest, "Checkpointed", [], []));
        var appended = store.AppendCheckpoint(current, initial.Checkpoint.Sequence,
            initial.Digest, "Checkpointed", [], []);
        Assert.Equal(2, appended.Checkpoint.Sequence);
        Assert.Equal(2, new PortableTaskLedger(fixture.Root).Read("T055").Count);
        Assert.Throws<InvalidOperationException>(() => store.AppendCheckpoint(old,
            2, appended.Digest, "Checkpointed", [], []));
    }

    [Fact]
    public void FencedCheckpointRejectsForgeryAndStaleHead()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var lease = store.Claim("T055", "agent-a", Duration);
        var initial = new PortableTaskLedger(fixture.Root).Read("T055")[^1];
        Assert.Throws<InvalidOperationException>(() => store.AppendCheckpoint(
            lease with { SecretToken = new string('f', 64) }, 1, initial.Digest,
            "Checkpointed", [], []));
        var appended = store.AppendCheckpoint(lease, 1, initial.Digest,
            "Checkpointed", [], []);
        Assert.Throws<InvalidOperationException>(() => store.AppendCheckpoint(lease,
            1, initial.Digest, "Checkpointed", [], []));
        store.Release(lease);
        Assert.Throws<InvalidOperationException>(() => store.AppendCheckpoint(lease,
            2, appended.Digest, "Checkpointed", [], []));
        Assert.Equal(2, new PortableTaskLedger(fixture.Root).Read("T055").Count);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "archpulse-t055-" + Guid.NewGuid().ToString("N"));
        public string LeasesPath => Path.Combine(Root, "T055", "leases");
        public ControlledClock Clock { get; } =
            new(new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero));
        public Fixture(bool withTask = true)
        {
            if (withTask)
                new PortableTaskLedger(Root).Append("T055", 0,
                    PortableTaskLedger.GenesisDigest, Envelope, "Planned", [], []);
        }
        public PortableTaskLeaseStore Store() => new(Root, Clock);
        public string Entry(int n) => Path.Combine(LeasesPath, $"{n:D8}.json");
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class ControlledClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
