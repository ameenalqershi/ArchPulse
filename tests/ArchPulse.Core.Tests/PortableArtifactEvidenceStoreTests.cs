// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortableArtifactEvidenceStoreTests
{
    private static readonly string Base = new('a', 40);
    private static readonly string Head = new('b', 40);
    private static readonly string Envelope = new('c', 64);

    [Fact]
    public void PortableManifestSurvivesRestartAndCanAnchorT054Checkpoint()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var diff = store.PutArtifact("T056", "patch", "git-diff", Head, "diff --git a/A.cs b/A.cs\n+test");
        var trx = store.PutArtifact("T056", "tests", "test-report", Head, "passed 3 failed 0");
        var receipt = store.PutManifest("T056", Base, Head, [diff, trx]);
        var reopened = fixture.Store().ReadManifest("T056", receipt.Sha256);
        Assert.Equal(receipt.Manifest, reopened.Manifest, new ManifestComparer());
        Assert.Equal("passed 3 failed 0", fixture.Store().GetArtifact("T056", trx));
        Assert.Equal(2, reopened.Manifest.Artifacts.Length);

        var ledger = new PortableTaskLedger(fixture.Path);
        var previous = ledger.Read("T056")[^1];
        var persisted = ledger.Append("T056", previous.Checkpoint.Sequence, previous.Digest,
            Envelope, "Checkpointed", [receipt.Sha256], []);
        Assert.Contains(receipt.Sha256, persisted.Checkpoint.EvidenceSha256);
        Assert.Equal(receipt.Sha256, fixture.Store().ReadManifest("T056", receipt.Sha256).Sha256);
    }

    [Fact]
    public void UnknownOrTamperedArtifactRejected()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var reference = store.PutArtifact("T056", "logs", "build-log", Head, "build passed");
        var receipt = store.PutManifest("T056", Base, Head, [reference]);
        File.WriteAllText(fixture.Artifact(reference.Sha256), "build FAILED");
        Assert.Throws<InvalidDataException>(() => store.ReadManifest("T056", receipt.Sha256));
        Assert.Throws<InvalidDataException>(() => store.GetArtifact("T056", reference));
    }

    [Fact]
    public void TamperedManifestCannotPassShaValidation()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var item = store.PutArtifact("T056", "logs", "build-log", Head, "build passed");
        var receipt = store.PutManifest("T056", Base, Head, [item]);
        var path = fixture.Manifest(receipt.Sha256);
        File.WriteAllText(path, File.ReadAllText(path).Replace(
            "\"headCommit\":\"" + Head + "\"", "\"headCommit\":\"" + Base + "\"",
            StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => store.ReadManifest("T056", receipt.Sha256));
    }

    [Fact]
    public void DuplicatePropertiesRejectedEvenWithRecomputedManifestHash()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var artifact = store.PutArtifact("T056", "logs", "build-log", Head, "passed");
        var receipt = store.PutManifest("T056", Base, Head, [artifact]);
        var original = File.ReadAllText(fixture.Manifest(receipt.Sha256));
        var injection = "\"taskId\":\"T999\",\"taskId\":\"T056\"";
        var malicious = original.Replace("\"taskId\":\"T056\"", injection, StringComparison.Ordinal);
        Assert.NotEqual(original, malicious);
        var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(malicious)));
        File.WriteAllText(fixture.Manifest(sha), malicious);
        Assert.Throws<InvalidDataException>(() => store.ReadManifest("T056", sha));
    }

    [Fact]
    public void DuplicateArtifactNamesRejected()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var reference = store.PutArtifact("T056", "tests", "test-report", Head, "passed");
        Assert.Throws<InvalidDataException>(() => store.PutManifest("T056", Base, Head,
            [reference, reference]));
    }

    [Fact]
    public void RevisionOutsideManifestEndpointsRejected()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var reference = store.PutArtifact("T056", "tests", "test-report", new string('d', 40), "passed");
        Assert.Throws<InvalidDataException>(() => store.PutManifest("T056", Base, Head, [reference]));
    }

    [Fact]
    public void SecretsAndUntrustedPathsRejectedBeforePersistence()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        Assert.Throws<InvalidDataException>(() => store.PutArtifact("T056", "logs",
            "build-log", Head, "the token is ghp_" + new string('A', 32)));
        Assert.Throws<InvalidDataException>(() => store.PutArtifact("T056", "logs",
            "build-log", Head, "-----BEGIN PRIVATE KEY-----\nDATA"));
        Assert.Throws<InvalidDataException>(() => store.PutArtifact("T056", "../escape",
            "build-log", Head, "passed"));
        Assert.Throws<InvalidDataException>(() => store.PutArtifact("T056", "logs",
            "../build", Head, "passed"));
        Assert.Throws<ArgumentException>(() => store.PutArtifact("../../etc", "logs",
            "build-log", Head, "passed"));
    }

    [Fact]
    public void ArtifactRequiresValidatedTaskLedger()
    {
        using var fixture = new Fixture(withLedger: false);
        Assert.Throws<InvalidDataException>(() => fixture.Store().PutArtifact("T056",
            "logs", "build-log", Head, "passed"));
    }

    [Fact]
    public void ReadManifestRejectsDeletedReferencedFile()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var item = store.PutArtifact("T056", "logs", "build-log", Head, "passed");
        var manifest = store.PutManifest("T056", Base, Head, [item]);
        File.Delete(fixture.Artifact(item.Sha256));
        Assert.Throws<InvalidDataException>(() => store.ReadManifest("T056", manifest.Sha256));
    }

    [Fact]
    public void ExistingArtifactMustRemainImmutableButExactDuplicateIsIdempotent()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var item = store.PutArtifact("T056", "logs", "build-log", Head, "passed");
        var second = store.PutArtifact("T056", "logs", "build-log", Head, "passed");
        Assert.Equal(item, second);
        var manifest = store.PutManifest("T056", Base, Head, [item]);
        Assert.Equal(manifest.Sha256, store.PutManifest("T056", Base, Head, [item]).Sha256);
        File.WriteAllText(fixture.Artifact(item.Sha256), "forged");
        Assert.Throws<InvalidDataException>(() => store.PutArtifact("T056", "logs",
            "build-log", Head, "passed"));
    }

    [Fact]
    public void CrashOrphanPendingArtifactsNeverCountAsReceipts()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var item = store.PutArtifact("T056", "logs", "build-log", Head, "passed");
        File.WriteAllText(Path.Combine(fixture.Path, "T056", "cas", ".orphan.pending"), "{partial");
        var manifest = store.PutManifest("T056", Base, Head, [item]);
        Assert.Equal(item.Sha256, store.ReadManifest("T056", manifest.Sha256).Manifest.Artifacts[0].Sha256);
    }

    [Fact]
    public void LinkedArtifactsAndManifestsRejected()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        var item = store.PutArtifact("T056", "logs", "build-log", Head, "passed");
        var manifest = store.PutManifest("T056", Base, Head, [item]);
        var file = fixture.Artifact(item.Sha256);
        File.Move(file, file + ".real");
        File.CreateSymbolicLink(file, file + ".real");
        Assert.Throws<InvalidDataException>(() => store.GetArtifact("T056", item));
        File.Delete(file);
        File.Move(file + ".real", file);
        var manifestFile = fixture.Manifest(manifest.Sha256);
        File.Move(manifestFile, manifestFile + ".real");
        File.CreateSymbolicLink(manifestFile, manifestFile + ".real");
        Assert.Throws<InvalidDataException>(() => store.ReadManifest("T056", manifest.Sha256));
    }

    [Fact]
    public async Task ConcurrentIdenticalWritersAreIdempotent()
    {
        using var fixture = new Fixture();
        var attempts = Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
            fixture.Store().PutArtifact("T056", "logs", "build-log", Head,
                "same redacted concurrent artifact"))).ToArray();
        var result = await Task.WhenAll(attempts);
        Assert.Single(result.Select(x => x.Sha256).Distinct());
        Assert.Single(Directory.EnumerateFiles(Path.Combine(fixture.Path, "T056", "cas"),
            "*.txt"));
    }

    [Fact]
    public void BoundedSizeAndManifestCount()
    {
        using var fixture = new Fixture();
        var store = fixture.Store();
        Assert.Throws<InvalidDataException>(() => store.PutArtifact("T056", "large",
            "build-log", Head, new string('x', PortableArtifactEvidenceStore.MaximumArtifactBytes + 1)));
        var first = store.PutArtifact("T056", "x", "test-report", Head, "passed");
        var tooMany = Enumerable.Range(0, 17).Select(i => first with { Name = "a" + i }).ToArray();
        Assert.Throws<InvalidDataException>(() => store.PutManifest("T056", Base, Head, tooMany));
    }

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-t056-" + Guid.NewGuid().ToString("N"));
        public Fixture(bool withLedger = true)
        {
            if (withLedger)
                new PortableTaskLedger(Path).Append("T056", 0,
                    PortableTaskLedger.GenesisDigest, Envelope, "Planned", [], []);
        }
        public PortableArtifactEvidenceStore Store() => new(Path);
        public string Artifact(string digest) => System.IO.Path.Combine(Path, "T056", "cas", digest + ".txt");
        public string Manifest(string digest) => System.IO.Path.Combine(Path, "T056", "manifests", digest + ".json");
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class ManifestComparer : IEqualityComparer<PortableArtifactManifest>
    {
        public bool Equals(PortableArtifactManifest? a, PortableArtifactManifest? b) =>
            a?.Schema == b?.Schema && a?.TaskId == b?.TaskId &&
            a?.EnvelopeSha256 == b?.EnvelopeSha256 &&
            a?.BaseCommit == b?.BaseCommit && a?.HeadCommit == b?.HeadCommit &&
            (a?.Artifacts ?? []).SequenceEqual(b?.Artifacts ?? []);
        public int GetHashCode(PortableArtifactManifest obj) => HashCode.Combine(obj.Schema, obj.TaskId);
    }
}
