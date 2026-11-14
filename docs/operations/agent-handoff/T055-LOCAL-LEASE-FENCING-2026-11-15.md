# T055/S11 — local lease epochs and opt-in checkpoint fencing

Date: 2026-11-15. Target: [ArchPulse](https://github.com/ameenalqershi/ArchPulse).
**Stacked parent:** [PR #75](https://github.com/ameenalqershi/ArchPulse/pull/75),
base SHA `caebcbd37d60257ce9cc4580d62c5e9c884c83c3`.
Source branch: `feat/t055-local-lease-fencing-20261115`.
Isolated VPS checkout: `/opt/archpulse-t055-fencing-20261115`.

## Scope and claims

- Native C# `PortableTaskLeaseStore`, appended as a separate local-only
  event chain under T054's checked task checkpoint directory. An opaque
  256-bit random token is returned to the caller; only SHA-256 of the
  token is persisted. Claims, renewals, releases and takeovers use
  exclusive cross-process local locks with a strictly advancing epoch
  on reassignment, time regression rejection, and bounded TTL (1s–10m).
- Current-lease checks compare token hash using constant-time equality,
  current epoch, agent, validated source envelope and full event chain.
- Opt-in `AppendCheckpoint` serializes lease validation and T054 checkpoint
  CAS/append under the same local lease lock, to reject an expired, released
  or replaced agent at the protected local checkpoint mutation.
- New xUnit tests include simultaneous claimants, expiry, takeover,
  stale renewal, old agent return, secret non-persistence, forged
  token/agent, clock rollback, JSON tampering, duplicate keys,
  orphan pending file, sequence gap, and stale locally fenced writes.
- No .NET product Python, paid service, new dependency, or changes
  to other agents' working trees.

## Explicit limitations and next actions

**Do not mark T055 Verified.** No remote or distributed resource fencing,
SQL-backed leases, multi-host quorum, independent approver verification,
hostile filesystem race resilience, cross-host clock behavior, hard-kill
crash durability or directory metadata fsync is proven. Public
`PortableTaskLedger.Append` remains a legacy **unfenced** route and
must not be exposed to untrusted agents. `IsCurrent` alone is
non-atomic with later writes. Only `AppendCheckpoint` is protected
from interleaving local lease claims during its mutation.

Recover: fetch exact PR head and hosted CI, run clean-clone
locked restore/Release build/full tests/validators, inspect human
architecture review, avoid merge before [PR #71](https://github.com/ameenalqershi/ArchPulse/pull/71)
and [PR #75](https://github.com/ameenalqershi/ArchPulse/pull/75).
T055 depends on incomplete T054/T053/T008, so follow-up
work should resolve the shared durable backend and require
fence checks at every protected write boundary, not merely preflight.
