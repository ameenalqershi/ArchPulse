# Experimental local task checkpoint ledger — S11/T054

This small, additive **single-host prototype** records source-neutral task
checkpoints in .NET 10. The [S11 specification](https://app.notion.com/p/4143830ba34d811193dcd9c2937ae915)
and [T054 task](https://app.notion.com/p/4143830ba34d811c9a82ea435e816f6b)
remain incompletely implemented, pending T053/T008 and architecture decisions.

## Contract

`PortableTaskLedger` operates under a **trusted, private, local** root
directory. Each task is a checked `Tnnn` identifier; checkpoint filenames are
`00000001.json`, `00000002.json`, etc. `Read(taskId)` verifies the
entire known committed sequence before returning an entry. Each entry has:

- Strict `archpulse.task-checkpoint/v1` schema and task ID.
- Monotonically increasing sequence, previous checkpoint SHA-256, fixed
  source envelope SHA-256 and content SHA-256.
- Claimed status: `Planned`, `Checkpointed`, `RecoveryRequired`,
  or `HandoffReady`. **No `CompletedVerified` status in this prototype.**
- Up to 32 content-addressed evidence hashes and 32 opaque unknown-effect IDs.

`Append(taskId, expectedSequence, expectedDigest, envelopeSha256, status,
evidenceSha256, unknownEffectIds)` takes a local cross-process exclusive
writer lock, validates existing history and optimistic head, writes one
unique `.pending` file with a file flush, then atomically renames to the
immutable numbered committed checkpoint (no overwrite). Crash-orphan
`.pending` files never become committed receipts. Stale expected heads,
corrupted or missing events, nested duplicate JSON keys and symlinked
committed events are rejected. Unknown-effect IDs **cannot disappear** from
subsequent checkpoints until a future independently verified reconciler is
implemented; `HandoffReady` may not contain unknown effects.

Example of caller flow (a separate operational service must validate task
policy, authorization and external effects before any agent operation):

```csharp
var journal = new PortableTaskLedger(trustedPrivateRoot);
var snapshot = journal.Read("T054");
var head = snapshot.Count == 0
    ? PortableTaskLedger.GenesisDigest : snapshot[^1].Digest;
var envelopeHash = "<64 lowercase hex characters for a checked envelope>";
var entry = journal.Append("T054", snapshot.Count, head, envelopeHash,
    "Checkpointed", [], []);
// Persisted entry is only a *claim* about the checkpoint, not verified work.
```

## Safety boundaries

**Not a transactional database / production durability guarantee.**
`FileStream.Flush(true)` and same-directory rename do not promise
directory metadata durability across sudden power loss on all filesystems.
Lost/removed complete history cannot be distinguished from a new task without
a separate trusted registry. SHA-256 is **tamper-evident against uncoordinated
changes**, not an authenticated signature: an attacker with write permission
can replace all historical events and recompute hashes. The root directory,
its permissions, its ancestor paths and local filesystem must be trusted;
there is no guarantee against a hostile concurrent filesystem actor or
symlink-race attack. Network filesystems may not respect local lock semantics.

There is no SQLite persistence/migration, multi-host store, lease/fencing
epoch, authenticated approval, CAS against a remote database, complete state
transition graph, external-effect executor, automated reconciliation,
test-run attribution, backup/retention, or safe remote retry.
The caller must **not** treat `HandoffReady`, evidence hashes, or effects
as authorization or independently verified results. No public or MCP write
endpoint was added. Complete T008/T053 design reviews and T055 fencing
before production adoption.

Validation: locked restore; Release build and full xUnit suite; focused
`dotnet test tests/ArchPulse.Core.Tests/ArchPulse.Core.Tests.csproj
--filter FullyQualifiedName~PortableTaskLedgerTests`; native repo/traceability
validators, scoped C# formatting verification and hosted CI.
