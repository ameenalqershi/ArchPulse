# T010/S02 — Verified local projection checkpoints (2026-11-14)

- Base main at branch cut: `344d17e4b56b9e18427b4f8177185751b3321b52`.
- Intent: Add opt-in local graph checkpoint creation and read-side replay
  acceleration without changing full event replay or Append concurrency.
- Code: `GraphRevisionJournal.CreateCheckpoint`,
  `ReplayFromLatestCheckpoint`; CLI `graph-journal-checkpoint` and
  `graph-journal-replay-checkpoint`; no remote write endpoint.
- Safety: before checkpoint creation, replay and validate *all* committed
  events under the writer lock; flush+same-directory atomic rename, immutable
  sequence-scoped file. Verify checkpoint schema, project, revision, source
  evidence, snapshot digest and finalized anchor event digest. Check
  contiguous filenames and the full post-checkpoint suffix during read.
- Trust boundary: older event *contents* are skipped in accelerated read;
  checkpoint SHA256 is only accidental-corruption detection, not signature.
  If older history changes after checkpoint creation, `Replay()` still detects
  corruption; checkpoint read may still recover a locally trusted latest
  graph. The latter is not full-history proof.
- Known gaps: no directory fsync after rename, no external durable signing,
  no compaction, no certified millions-of-edges load, no multi-host lock.
  T010 stays In Progress, S02 remains Proposed.
- Reproduced local on Linux VPS .NET 10 SDK (branch based on `344d17e4`):
  `dotnet restore ArchPulse.sln --locked-mode` PASS; `dotnet build ArchPulse.sln -c Release --no-restore` PASS (0 warnings/errors);
  complete xUnit solution **312/312 PASS** (new GraphRevisionJournalTests **21/21 PASS**).
  `RepositoryValidator --adr`, `TraceabilityValidator --baseline`,
  scoped `dotnet format whitespace --verify-no-changes` PASS.
  CLI smoke in an isolated copy of Billing journal: create idempotent checkpoint
  at `r2`, exact byte equality between full replay and checkpoint read,
  and exactly one checkpoint file PASS.
- Exact GitHub SHA, PR and hosted CI must be verified after publishing. Do not infer success.
