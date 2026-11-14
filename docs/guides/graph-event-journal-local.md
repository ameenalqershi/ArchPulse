# T010/S02 — Local graph revision journal (experimental)

The existing immutable `GraphSnapshotStore` remains the source of exact indexed
revision documents for local HTTP, MCP, Web Studio and other consumers. This
additional local-only projection allows trusted CLI operators to commit
append-only graph revisions with an explicit expected head and to rebuild
the latest graph from the journal alone.

**Not production-ready**: This is a bounded first implementation of T010,
not completed event sourcing, database durability, migration, authentication
or crash-safe cross-agent orchestration.

## Workflow (trusted local .NET solution only)

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  index-solution examples/billing-multi-project/Billing.sln --revision demo-r1 --store /tmp/ap-graph-snapshots
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  graph-journal-append Billing --revision demo-r1 --store /tmp/ap-graph-snapshots --journal /tmp/ap-event-journal
# Edit the trusted fixture and reindex with a DIFFERENT revision ID, then:
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  graph-journal-append Billing --revision demo-r2 --store /tmp/ap-graph-snapshots \
  --journal /tmp/ap-event-journal --expected demo-r1
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  graph-journal-show Billing --journal /tmp/ap-event-journal
# View the prior exact revision without rebuilding or changing the HEAD:
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  graph-journal-show Billing --journal /tmp/ap-event-journal --revision demo-r1
# After fully validating the committed event chain, create an immutable local checkpoint:
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  graph-journal-checkpoint Billing --journal /tmp/ap-event-journal
# Explicit checkpoint-anchored read skips prior event payload verification:
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  graph-journal-replay-checkpoint Billing --journal /tmp/ap-event-journal
```

The second example requires that `demo-r2` was already indexed; a missing
snapshot is a hard failure, not a synthetic event. Source index only from
trusted repositories, never arbitrary user-submitted MSBuild projects.

## Invariants and recovery

- One folder per SHA-256 of exact project name; no project-derived path segments.
- Event `0000000000000001.event.json` is an initial full graph baseline;
  subsequent events store changed/added nodes and edges plus removals, exact
  revision diagnostics and the prior event's SHA-256 digest.
- On-disk event JSON rejects duplicate property names at any object depth before deserialization; a last-value-wins parser cannot conceal ambiguous event fields behind an unchanged reconstructed digest.
- Every replay verifies sequential filenames, schema/version, project ID,
  expected parent revision and parent digest, event digest and resulting
  `GraphSnapshotStore.Validate`. Source evidence is rebound to its exact
  revision even when only the revision changed.
- Repeated append of an identical head is idempotent; a reused revision with
  different data or a wrong expected head is rejected.
- A persistent writer-lock file and `FileShare.None` reject competing writers;
  a flushed temporary file is renamed into place atomically on the same
  local filesystem. Abandoned `.tmp` files are ignored, not treated as events.
- `ReplayAt(project, revision)` validates and replays **only the prefix** through
  that exact committed revision; it returns null for unknown revisions. A
  corrupted newer event does not block recovering a previously valid revision,
  but this does **not** certify the entire later journal. `Replay(project)`
  continues validating all committed events and fails on corrupted newer data.
- Corrupt, missing or reordered committed events in the requested prefix
  fail closed. A loss of an **entire final event** cannot be distinguished
  from a never committed event
  without an external durable checkpoint; this is a known limitation.
- Identity and event hashes provide **accidental-tamper detection**, not
  authentication against a local actor able to rewrite every event and digest.
- Optional checkpoint files are `<sequence>.checkpoint.json` containing a
  complete validated projection, event-chain anchor digest and snapshot digest.
  They are finalized by a flushed temporary file + same-directory atomic
  rename and cannot replace existing differing content.
- `CreateCheckpoint()` first fully validates the journal and takes the same
  local writer lock as Append. `ReplayFromLatestCheckpoint()` verifies the
  newest checkpoint identity, snapshot digest, canonical Graph validity and
  exact anchor event digest; validates event filenames and all subsequent
  event payloads. A corrupt checkpoint **fails** rather than using an older
  checkpoint silently.
- The checkpoint is a **local trust boundary**, NOT proof that unchanged
  pre-checkpoint event bytes remain intact. A full `Replay()` still audits
  every committed event; `Append()` also always replays the full history
  before making any mutation. No authenticated or signed checkpoints.
- No compaction, checkpoint rotation, bounded historical query cost,
  reader isolation guarantee beyond immutable renamed events, directory fsync,
  network filesystem guarantee, runtime observation, multi-host lease, or
  performance proof at millions of edges yet.

## Evidence and test criteria

Use `GraphRevisionJournalTests` for delta/source preservation, all-new
baseline, stable-ID revision-only advance, optimistic concurrency, duplicate
retry, malformed/corrupt chain, missing history, crash temp and project
isolation. Exact independent branch and CI outcomes are recorded in the
T010 handoff. S02 remains Proposed, T010 In Progress until approval,
performance/load testing and further acceptance evidence.
