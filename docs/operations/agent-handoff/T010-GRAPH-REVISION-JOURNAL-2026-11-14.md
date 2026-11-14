# T010/S02 — Graph revision journal handoff (2026-11-14)

- Starting revision: `75c7b3ddc13c536f8abe4cd346ee8136c1d19b88`.
- Work branch: `feat/t010-graph-event-journal-20261114`.
- Scope: local-only additive event journal with CAS, replay, mutation
  isolation, CLI append/show and tests. Existing snapshot store unchanged.
- Evidence: see `docs/guides/graph-event-journal-local.md` and proposed
  `docs/decisions/ADR-0015-graph-revision-journal-PROPOSED.md`.
- Assumptions: local trusted filesystem and operator; remote writer
  auth, multi-host durability, network filesystems and DB-like guarantees
  not provided.
- Unverified: full T010/S02 acceptance, directory fsync, crash after
  finalization with whole-event loss, million-edge workload, compaction,
  authenticated audit, agent lease integration.
- Rollback: revert additive files/CLI branch without changing prior
  snapshot content; do not delete any user journal directories.
- The exact commit, test totals, hosted CI and PR are to be attached only
  after they are observed; do not infer success from this handoff.
