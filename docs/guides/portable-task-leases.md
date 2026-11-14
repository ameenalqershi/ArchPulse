# Local task-lease fencing prototype — S11/T055

**Status: experimental, single-host, read-only fencing observation.**
This is an incremental *partial* implementation of [T055](https://app.notion.com/p/4143830ba34d81b0bfb8fba60925f6b1)
based on the [T054 prototype](portable-task-ledger.md) and the
[T053 v1 contracts](portable-handoff-contracts.md). It is **not** a
production agent coordinator or permission service.

## Local API and behavior

`PortableTaskLeaseStore(trustedPrivateRoot, optionalTimeProvider)` uses a
validated T054 task checkpoint as the task identity and source-envelope anchor.
A lease cannot start without at least one valid task checkpoint.

- `Claim(taskId, agentId, ttl)`: acquire an exclusive local claim if no
  unexpired claim exists. Issues epoch 1 for a new task and increases it
  for each successful takeover/reassignment. TTL is 1 second to 10 minutes.
- `Renew(grant, ttl)`: same owner, random 256-bit secret and epoch;
  only before expiry, strictly extending expiration. No new epoch.
- `Release(grant)`: checked owner/secret/epoch, before expiration;
  closes that lease. A new caller can claim a strictly higher epoch.
- `IsCurrent(grant)`: **read-only instantaneous observation**, checking
  epoch, owner, hashed secret, deadline, task envelope and full journal
  integrity. The result can become stale immediately after return.
- `AppendCheckpoint(grant, expectedSequence, expectedDigest, ...)`:
  optional **locally fenced** T054 checkpoint write. It holds the shared
  lease lock while checking the epoch and performing the T054 append/CAS;
  a takeover cannot interleave with this method's protected checkpoint.
  **Legacy direct `PortableTaskLedger.Append` remains unfenced** and can
  bypass this protection. Callers must use and enforce the fenced API.
- `ReadEvents(taskId)`: verifies a numbered append-only SHA-256 linked
  event log, including legal transitions, time monotonicity, expiry bounds,
  duplicate keys and unknown properties.

The writer uses `FileShare.None` for an exclusive *local* process lock,
writes a unique `.pending` file, flushes the file and atomically renames
without overwriting numbered events. Crash-orphan `.pending` files are
not accepted as committed receipts. Raw lease secrets are **not stored**
in events (only their SHA-256 digests); callers must avoid logging secrets.
An invalid lock/revision/corrupt event fails closed. Clock regressions
relative to committed event times fail closed.

**Security invariant in this bounded prototype:** A replaced agent cannot
renew, release, or pass a fresh `IsCurrent` observation using its former
(epoch, secret, owner). Two simultaneous claimants cannot both receive a
committed epoch-1 claim on a correctly locking local filesystem.

## Critical deployment limits

This implementation **does not fence external writes**. The new
`AppendCheckpoint` fences its **own local checkpoint mutation** by holding
one lease lock across the epoch check and append. It does not protect
legacy direct `PortableTaskLedger.Append` calls, nor arbitrary file, Git,
database or remote writes. In particular, `IsCurrent` followed by a Git
push, issue edit, database write or remote deployment has a race: ownership
may change between checking and writing. Protected external systems must
**atomically reject stale epochs at their own mutation boundary**,
which T055 has **not yet implemented**. Never expose these
objects as agent permission grants, or infer exactly-once effects from
their success. The local process lock is not a distributed mutex.
No multi-host coordination, transactional SQLite/PostgreSQL backend,
cross-filesystem fencing, authenticated identities, approval policy,
secure capability exchange, tested network partition, hard-kill or
full power-loss durability guarantee exists.

The trusted filesystem and its ancestors must be protected against hostile
writes, symlinks and rollback. Anyone who can rewrite all numbered files
and recompute hashes can fabricate or rewind epochs. File flush+rename is
not proof of durable directory metadata on unexpected power failure.
A process may outlive its expired lease, and wall-clock TTLs do not solve
large clock jumps or cross-host clock disagreement. Security design review,
T008 persistence policy and T054 acceptance are still open.
This is neither a stable public API nor connected to an MCP/HTTP write API.

## Reproduction

```sh
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build
dotnet test tests/ArchPulse.Core.Tests/ArchPulse.Core.Tests.csproj \
  --filter FullyQualifiedName~PortableTaskLeaseStoreTests
```

Do not mark T055 Verified from these tests alone. The next architectural
slice should make protected local writes and lease event advancement
one transaction with a durable external epoch registry, then test agent
crashes and stale mutation rejection at the actual write boundary.
