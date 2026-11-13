# T017 / S02 — canonical ownership validator scalability (2026-11-13)

- Baseline: GitHub `main` commit
  `2e755869c672caf384c200106534f1e1cecab658` at branch creation.
- Branch: `feat/t017-linear-hierarchy-validation-20261113`.
- Trusted isolated Linux VPS checkout:
  `/tmp/archpulse-agent-graph-scale-20261113`; do not overwrite other
  agents' working trees.
- Problem: `GraphSnapshotStore.Validate` formerly rescanned ownership
  ancestors for every node, O(N²) on a valid deep chain.
- Change: iterative visit/resolve across the canonical parent forest,
  O(N) parent traversal, preserving cycle detection and error behavior.
- Test corpus: 30k deep chain (reverse order), 18k deep cycle and 10k
  branched multi-root forest, in `GraphSnapshotStoreTests`.
- Read `docs/guides/graph-validation-scale.md` for usage, limits and
  code-level algorithmic bound.
- Independently verify exact SHA and hosted CI after publication; record
  `dotnet restore`, Release build, full xUnit, native validators, and
  repository PR review status.
- T017 remains partial: no 100k+ source indexing benchmark, backend
  pagination, high-density UI virtualization, memory/latency SLO proof,
  or T015/T011 accepted prerequisites. No production or S02 approval.
- Next agent: check latest main, remote PR SHA and CI before editing,
  merge only with required human architectural review. Preserve evidence
  `Designed` vs `Static` vs `Observed` vs `Verified`.
