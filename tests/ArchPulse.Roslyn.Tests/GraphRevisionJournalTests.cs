// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class GraphRevisionJournalTests
{
    private static GraphSnapshot Graph(string revision, string source, string project = "Invoices") =>
        StaticGraphIndexer.IndexSources(project,
            new Dictionary<string, string> { ["Billing.cs"] = source }, revision);

    [Fact]
    public void ReplaysBaselineAndSourceBackedDeltasWithoutLosingExactRevisionEvidence()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var first = Graph("r1", "public class A { public int One() => 1; }");
        var second = Graph("r2", "public class A { public int One() => Two(); public int Two() => 2; }");
        var third = Graph("r3", "public class A { public int Two() => 2; }");
        var r1 = journal.Append(first, null);
        var r2 = journal.Append(second, "r1");
        var r3 = journal.Append(third, "r2");
        Assert.Equal(1, r1.Sequence);
        Assert.Equal(2, r2.Sequence);
        Assert.Equal(3, r3.Sequence);
        Assert.NotEqual(r1.Digest, r2.Digest);
        Assert.NotEqual(r2.Digest, r3.Digest);
        var recovered = Assert.IsType<GraphSnapshot>(new GraphRevisionJournal(temp.Path).Replay("Invoices"));
        GraphSnapshotStore.Validate(recovered);
        Assert.Equal(third.Nodes.OrderBy(n => n.SymbolId).Select(n => n),
            recovered.Nodes.OrderBy(n => n.SymbolId).Select(n => n));
        Assert.Equal(third.Edges.OrderBy(e => e.EdgeId).Select(e => e),
            recovered.Edges.OrderBy(e => e.EdgeId).Select(e => e));
        Assert.Equal("r3", recovered.Revision);
        Assert.All(recovered.Nodes, node => Assert.Equal("r3", node.Evidence.Revision));
        Assert.Equal(third.Diagnostics, recovered.Diagnostics);
    }

    [Fact]
    public void UnchangedStructureCanAdvanceRevisionWithoutRewritingNodes()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}"), null);
        var receipt = journal.Append(Graph("r2", "public class A {}"), "r1");
        var evt = File.ReadAllText(receipt.EventPath);
        Assert.Contains("\"UpsertNodes\":[]", evt, StringComparison.Ordinal);
        var recovered = Assert.IsType<GraphSnapshot>(journal.Replay("Invoices"));
        Assert.Equal("r2", recovered.Revision);
        Assert.All(recovered.Nodes, n => Assert.Equal("r2", n.Evidence.Revision));
    }

    [Fact]
    public void RetriesAreIdempotentAndExpectedRevisionConflictNeverWritesAnEvent()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var first = Graph("r1", "public class A {}");
        var firstReceipt = journal.Append(first, null);
        Assert.Equal(firstReceipt, journal.Append(first, null));
        Assert.Throws<InvalidOperationException>(() => journal.Append(
            Graph("r1", "public class B {}"), null));
        Assert.Throws<InvalidOperationException>(() => journal.Append(
            Graph("r2", "public class A {}"), null));
        var next = journal.Append(Graph("r2", "public class A {}"), "r1");
        Assert.Equal(next, journal.Append(Graph("r2", "public class A {}"), "r1"));
        Assert.Equal(2, Directory.GetFiles(System.IO.Path.GetDirectoryName(next.EventPath)!,
            "*.event.json").Length);
    }

    [Fact]
    public void UnfinishedTemporaryFileIsIgnoredAfterCrashAndReplayCanContinue()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var r1 = journal.Append(Graph("r1", "public class A {}"), null);
        File.WriteAllText(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(r1.EventPath)!, ".archpulse-interrupted.tmp"), "{");
        Assert.Equal("r1", journal.Replay("Invoices")!.Revision);
        Assert.Equal(2, journal.Append(Graph("r2", "public class A {}"), "r1").Sequence);
    }

    [Fact]
    public void DetectsTamperedFinalEventAndRefusesToAppendAfterCorruption()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var first = journal.Append(Graph("r1", "public class A {}"), null);
        var second = journal.Append(Graph("r2", "public class B {}"), "r1");
        string text = File.ReadAllText(first.EventPath);
        Assert.Contains("\"Revision\":\"r1\"", text, StringComparison.Ordinal);
        File.WriteAllText(first.EventPath, text.Replace("\"Revision\":\"r1\"",
            "\"Revision\":\"r9\"", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => journal.Replay("Invoices"));
        Assert.Throws<InvalidDataException>(() => journal.Append(
            Graph("r3", "public class C {}"), "r2"));
        Assert.True(File.Exists(second.EventPath));
    }

    [Fact]
    public void MissingMiddleEventIsDetectedRatherThanSilentlyReplayingWrongHistory()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}"), null);
        var middle = journal.Append(Graph("r2", "public class B {}"), "r1");
        journal.Append(Graph("r3", "public class C {}"), "r2");
        File.Delete(middle.EventPath);
        Assert.Throws<InvalidDataException>(() => journal.Replay("Invoices"));
    }

    [Fact]
    public void IndependentProjectsDoNotShareRevisionsOrWriterLock()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}", "ServiceA"), null);
        journal.Append(Graph("r1", "public class B {}", "ServiceB"), null);
        Assert.Equal("ServiceA", journal.Replay("ServiceA")!.Project);
        Assert.Equal("ServiceB", journal.Replay("ServiceB")!.Project);
        Assert.Null(journal.Replay("not-indexed"));
    }

    [Fact]
    public async Task ConcurrentWritersCannotBothCommitAgainstTheSameExpectedHead()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var firstReceipt = journal.Append(Graph("r1", "public class A {}"), null);
        var competitors = new[]
        {
            Graph("r2a", "public class A { public void One() {} }"),
            Graph("r2b", "public class A { public void Two() {} }")
        };
        var outcomes = await Task.WhenAll(competitors.Select(snapshot =>
            Task.Run(() =>
            {
                try { journal.Append(snapshot, "r1"); return true; }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                { return false; }
            })));
        Assert.Single(outcomes, value => value);
        Assert.Equal(2, Directory.GetFiles(System.IO.Path.GetDirectoryName(
            firstReceipt.EventPath)!, "*.event.json").Length);
    }

    [Fact]
    public void HistoricalReplayReturnsExactSourceEvidenceForEachCommittedRevision()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var first = Graph("r1", "public class A { public int One() => 1; }");
        var second = Graph("r2", "public class A { public int One() => 2; public void Two() {} }");
        var third = Graph("r3", "public class B {}");
        journal.Append(first, null);
        journal.Append(second, "r1");
        journal.Append(third, "r2");

        var earlier = Assert.IsType<GraphSnapshot>(journal.ReplayAt("Invoices", "r1"));
        var middle = Assert.IsType<GraphSnapshot>(journal.ReplayAt("Invoices", "r2"));
        var head = Assert.IsType<GraphSnapshot>(journal.Replay("Invoices"));
        Assert.Equal(first.Nodes.OrderBy(n => n.SymbolId), earlier.Nodes);
        Assert.Equal(second.Nodes.OrderBy(n => n.SymbolId), middle.Nodes);
        Assert.Equal("r3", head.Revision);
        Assert.All(earlier.Nodes, n => Assert.Equal("r1", n.Evidence.Revision));
        Assert.All(middle.Nodes, n => Assert.Equal("r2", n.Evidence.Revision));
        Assert.NotEqual(head.Nodes.Select(n => n.SymbolId), earlier.Nodes.Select(n => n.SymbolId));
    }

    [Fact]
    public void HistoricalReplayReturnsNullForUnknownRevisionWithoutGuessingHead()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        Assert.Null(journal.ReplayAt("Invoices", "r1"));
        Assert.Throws<ArgumentException>(() => journal.ReplayAt("Invoices", "  "));
        journal.Append(Graph("r1", "public class A {}"), null);
        Assert.Null(journal.ReplayAt("Invoices", "r99"));
        Assert.Equal("r1", journal.Replay("Invoices")!.Revision);
    }

    [Fact]
    public void HistoricalReplayFailsClosedIfRequiredPrefixIsCorrupt()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var first = journal.Append(Graph("r1", "public class A {}"), null);
        journal.Append(Graph("r2", "public class B {}"), "r1");
        File.WriteAllText(first.EventPath, "{malformed-event");
        Assert.Throws<InvalidDataException>(() => journal.ReplayAt("Invoices", "r2"));
    }

    [Fact]
    public void HistoricalRecoveryCanReadValidPrefixWhenUnrelatedNewerEventIsCorrupt()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}"), null);
        var second = journal.Append(Graph("r2", "public class B {}"), "r1");
        File.WriteAllText(second.EventPath, "{malformed-event");
        Assert.Equal("r1", journal.ReplayAt("Invoices", "r1")!.Revision);
        Assert.Throws<InvalidDataException>(() => journal.Replay("Invoices"));
        Assert.Throws<InvalidDataException>(() =>
            journal.Append(Graph("r3", "public class C {}"), "r1"));
    }


    [Fact]
    public void CheckpointCanReplayLaterDeltasWithExactGraphAndProvenance()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}"), null);
        journal.Append(Graph("r2", "public class A { public int Two() => 2; }"), "r1");
        var checkpoint = journal.CreateCheckpoint("Invoices");
        var latest = Graph("r3", "public class A { public int Two() => 3; public void Three() {} }");
        journal.Append(latest, "r2");
        var restored = Assert.IsType<GraphSnapshot>(
            new GraphRevisionJournal(temp.Path).ReplayFromLatestCheckpoint("Invoices"));
        var fullyReplayed = Assert.IsType<GraphSnapshot>(journal.Replay("Invoices"));
        Assert.Equal("r2", checkpoint.Revision);
        Assert.Equal(2, checkpoint.Sequence);
        Assert.Equal(latest.Nodes.OrderBy(n => n.SymbolId), restored.Nodes);
        Assert.Equal(latest.Edges.OrderBy(e => e.EdgeId), restored.Edges);
        Assert.Equal(fullyReplayed.Nodes.OrderBy(n => n.SymbolId),
            restored.Nodes.OrderBy(n => n.SymbolId));
        Assert.Equal(fullyReplayed.Edges.OrderBy(e => e.EdgeId),
            restored.Edges.OrderBy(e => e.EdgeId));
        Assert.Equal(fullyReplayed.Diagnostics, restored.Diagnostics);
        Assert.All(restored.Nodes, node => Assert.Equal("r3", node.Evidence.Revision));
    }

    [Fact]
    public void CheckpointIsImmutableIdempotentAndNewerCheckpointBecomesLatest()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var first = journal.Append(Graph("r1", "public class A {}"), null);
        var cp1 = journal.CreateCheckpoint("Invoices");
        Assert.Equal(first.Digest, cp1.EventDigest);
        Assert.Equal(cp1, journal.CreateCheckpoint("Invoices"));
        journal.Append(Graph("r2", "public class B {}"), "r1");
        var cp2 = journal.CreateCheckpoint("Invoices");
        Assert.Equal(2, cp2.Sequence);
        Assert.NotEqual(cp1.CheckpointPath, cp2.CheckpointPath);
        Assert.Equal("r2", journal.ReplayFromLatestCheckpoint("Invoices")!.Revision);
        Assert.Equal(2, Directory.GetFiles(System.IO.Path.GetDirectoryName(
            cp2.CheckpointPath)!, "*.checkpoint.json").Length);
    }

    [Fact]
    public void CheckpointReadRejectsModifiedSnapshotAndDoesNotFallBackSilently()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}"), null);
        var cp = journal.CreateCheckpoint("Invoices");
        var content = File.ReadAllText(cp.CheckpointPath);
        Assert.Contains("\"SnapshotDigest\":", content, StringComparison.Ordinal);
        File.WriteAllText(cp.CheckpointPath,
            content.Replace("\"SnapshotDigest\":\"", "\"SnapshotDigest\":\"bad",
                StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(
            () => journal.ReplayFromLatestCheckpoint("Invoices"));
        Assert.Throws<InvalidDataException>(() => journal.CreateCheckpoint("Invoices"));
        Assert.Equal("r1", journal.Replay("Invoices")!.Revision);
    }

    [Fact]
    public void CheckpointRejectsDamagedAnchorEvenWhenSnapshotBytesAreIntact()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}"), null);
        var anchor = journal.Append(Graph("r2", "public class B {}"), "r1");
        journal.CreateCheckpoint("Invoices");
        File.WriteAllText(anchor.EventPath, "{broken-anchor");
        Assert.Throws<InvalidDataException>(
            () => journal.ReplayFromLatestCheckpoint("Invoices"));
    }

    [Fact]
    public void CheckpointReadRejectsMissingHistoricalEventFilenameAndMissingTail()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var earliest = journal.Append(Graph("r1", "public class A {}"), null);
        journal.Append(Graph("r2", "public class B {}"), "r1");
        journal.CreateCheckpoint("Invoices");
        journal.Append(Graph("r3", "public class C {}"), "r2");
        File.Delete(earliest.EventPath);
        Assert.Throws<InvalidDataException>(
            () => journal.ReplayFromLatestCheckpoint("Invoices"));
    }

    [Fact]
    public void CheckpointReadRejectsCorruptedTailAndAppendNeverUsesUnverifiedCheckpoint()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        journal.Append(Graph("r1", "public class A {}"), null);
        journal.CreateCheckpoint("Invoices");
        var latest = journal.Append(Graph("r2", "public class B {}"), "r1");
        File.WriteAllText(latest.EventPath, "{bad-tail");
        Assert.Throws<InvalidDataException>(() => journal.ReplayFromLatestCheckpoint("Invoices"));
        Assert.Throws<InvalidDataException>(
            () => journal.Append(Graph("r3", "public class C {}"), "r2"));
    }

    [Fact]
    public void CheckpointFastPathCanReadLocalAnchorWhenOlderPayloadIsCorruptButFullAuditFails()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var first = journal.Append(Graph("r1", "public class A {}"), null);
        journal.Append(Graph("r2", "public class B {}"), "r1");
        journal.CreateCheckpoint("Invoices");
        journal.Append(Graph("r3", "public class C {}"), "r2");
        File.WriteAllText(first.EventPath, "{corrupted-pre-checkpoint-event");
        // A checkpoint is a *local trust boundary*, not proof of the original
        // earlier event contents or a substitute for the full replay audit.
        Assert.Equal("r3", journal.ReplayFromLatestCheckpoint("Invoices")!.Revision);
        Assert.Throws<InvalidDataException>(() => journal.Replay("Invoices"));
        Assert.Throws<InvalidDataException>(() => journal.CreateCheckpoint("Invoices"));
    }

    [Fact]
    public void CheckpointReadFallsBackToFullReplayWhenNoCheckpointExists()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        Assert.Null(journal.ReplayFromLatestCheckpoint("Invoices"));
        Assert.Throws<FileNotFoundException>(() => journal.CreateCheckpoint("Invoices"));
        journal.Append(Graph("r1", "public class A {}"), null);
        Assert.Equal(journal.Replay("Invoices")!.Nodes.OrderBy(n => n.SymbolId),
            journal.ReplayFromLatestCheckpoint("Invoices")!.Nodes.OrderBy(n => n.SymbolId));
    }

    [Fact]
    public void CheckpointIgnoresAbandonedTemporaryWritesWithoutPublishingPartialState()
    {
        using var temp = new JournalDirectory();
        var journal = new GraphRevisionJournal(temp.Path);
        var receipt = journal.Append(Graph("r1", "public class A {}"), null);
        File.WriteAllText(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(receipt.EventPath)!, ".archpulse-orphan.tmp"), "{");
        var checkpoint = journal.CreateCheckpoint("Invoices");
        Assert.True(File.Exists(checkpoint.CheckpointPath));
        Assert.Equal("r1", journal.ReplayFromLatestCheckpoint("Invoices")!.Revision);
    }

    private sealed class JournalDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-t010-" + Guid.NewGuid().ToString("N"));
        public JournalDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
