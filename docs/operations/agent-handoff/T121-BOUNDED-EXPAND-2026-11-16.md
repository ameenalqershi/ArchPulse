# T121/S27 — Bounded canonical expansion

- Independent checkout: `/tmp/archpulse-t121-bounded-expansion-20261116`
- Base SHA: `f6721647feee15e317e05484fe8bb2ba63b4a617`
- Branch: `feat/t121-bounded-canonical-expand-20261116`
- Scope: `BoundedCanonicalExpansion` driven by validated exact revision
  `LocalViewScopeDescriptor`; iterative BFS over canonical ParentSymbolId,
  maxNodes/maxEdges/depth, flags when partial, original source evidence,
  no continuation token and no authorization.
- New tests: four Graph xUnit cases, including exact BFS, node/edge
  budget, revision mismatch and descriptor forgery.
- Local gates observed: full 351/351 .NET tests, ADR and Traceability
  baseline validators, scoped whitespace and diff check succeeded.
- Not yet tested in an accepted production ViewScope, not an approved
  architecture; ADR-0020 Proposed.
- Must verify hosted CI against the exact published commit before merge;
  preserve all other agents' branches/PRs. Do not mark T121 Verified.
