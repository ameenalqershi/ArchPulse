# T119/S27 — Source-backed scope topology handoff

- Branch: `feat/t119-canonical-scope-topology-20261115`.
- Base SHA: `514d3fad1cb2bd6198a769888893505031b27fd5`
  (merged #76, observed Foundation CI, CodeQL and ADR governance pass).
- Independent worktree: `/tmp/archpulse-t119-topology-20261115`.
- No changes to concurrent PRs #68, #71, #72, #73, #74, #75.
- Scope: additive CanonicalHierarchyTopology and exact-revision local
  read-only `scope-topology` GET with depth/root/breadcrumb/direct child
  evidence/descendant counts, iterative construction, bounded 128-node
  breadcrumb, and binary-search child page over one in-memory projection.
- Security: evidence kinds preserved; no inferred->verified promotion,
  no untrusted compilation, no remote listener/mutation, no implicit
  authorization claim. T009/T011 dependency acceptance incomplete.
- Tests: L0–L6 synthetic hierarchy, multi-root and cross-scope call exclusion,
  renamed display label with stable SymbolIds across revisions, 30k breadth,
  30k depth without stack overflow, evidence counters, corrupt ownership,
  HTTP success and fail-closed cases.
- Proposed ADR-0017 with two Mermaid alternatives documents dependency
  constraints and future persistent indexed ViewScope decision.
- Record exact commit, hosted checks and merge only after observed.
- T119 remains In Progress/Proposed prototype, NOT Verified.
