# T054/S11 — experimental local immutable task checkpoint ledger

Date: 2026-11-14; parent branch: `feat/t053-portable-task-envelope-v1-20261114`,
exact base `5a277916295abc3088d3e99b80528eda43ea3e8c`.
Feature branch: `feat/t054-immutable-task-ledger-20261114`.
Isolated trusted VPS checkout: `/opt/archpulse-t054-journal-20261114`.
Do not change other agents' open PRs, worktrees or production listeners.

## Implemented, bounded

- `engine/ArchPulse.Core/PortableTaskLedger.cs`: task-specific local append-only
  numbered checkpoints with SHA-256 hash linkage, exact source-envelope digest
  and optimistic expected-head sequence/digest CAS.
- Cross-process exclusive local writer lock; create-new flushed pending file
  and no-overwrite rename; orphan `.pending` excluded from committed recovery.
- Fail-closed read on missing event, corrupt hash, noncanonical/duplicate JSON,
  bad task ID, linked checkpoint, unknown statuses, duplicate evidence references,
  oversized checkpoint. Unknown external effects are monotonically retained
  until a separately verified reconciler exists. Claims do not prove execution.
- `tests/ArchPulse.Core.Tests/PortableTaskLedgerTests.cs`: 12 focused xUnit
  tests including restart recovery, two-agent race, corrupt record, duplicate
  key, sequence gap, crash orphan, stale writer, fixed envelope and unknown effects.
- `docs/guides/portable-task-ledger.md`: consumer and safety guide.

## Reproducible evidence

- `dotnet format whitespace ArchPulse.sln --verify-no-changes --no-restore --include ...`
  for the two new C# files: PASS.
- `dotnet restore ArchPulse.sln --locked-mode`: PASS.
- `dotnet build ArchPulse.sln -c Release --no-restore`: PASS,
  **0 warnings / 0 errors**.
- `dotnet test ArchPulse.sln -c Release --no-build`:
  **330/330 PASSED** across 8 test assemblies, no skip/fail.
- Native RepositoryValidator and Traceability `--baseline`: PASS.
- Scope is additive: new source, tests and docs, no shared worktree changes.

## Explicit gaps and receiver instructions

This is **not** full T054, T055, S11, T008 or T053 acceptance.
T054 depends on proposed T053 and T008; design decisions remain open.
Directory fsync across sudden power loss, whole-history substitution, local
hostile filesystem actors, authenticated agents/approvals, external effect
reconciliation, state machine transitions, leases/epochs/fencing, SQL migration
and multi-host transactions are not solved.

The next agent must inspect hosted CI on the exact published head, verify
GitHub SHA in an independent clean clone and record those receipts, then
request architecture review. Never merge the stacked PR before its base PR
or mark T054 Verified. No external effect may be retried just because its
checkpoint lacks an authenticated receipt.
