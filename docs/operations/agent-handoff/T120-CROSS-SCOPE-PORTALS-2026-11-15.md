# T120/S27 — Cross-scope portal experiment handoff (2026-11-15)

- Branch: `feat/t120-cross-scope-portal-query-20261115`.
- Base main SHA: `c3da7c01de070c66b37716dd30f9990ae72afd04`
  (PR #77 merged; all post-merge main workflow checks observed successful).
- Isolated checkout: `/tmp/archpulse-t120-portals-20261115`.
- Independent files: `engine/ArchPulse.Graph/CrossScopePortalQuery.cs`,
  `server/ArchPulse.Server/Program.cs`, two new xUnit test files,
  this handoff, guide, proposed ADR. Never reset or modify other agents'
  branches and open PRs #68, #71-75, #78-80.
- Scope: immutable exact revision graph, canonical ownership subtree,
  boundary edge direction and evidence, bounded keyset and scan budget.
  Explicit `AuthorizationNotEvaluated` and `NotIndexed` statuses prevent
  accidental conflation with ViewScope or feature membership.
- Tests: 3-module source fixture, exact inbound/outbound and evidence,
  external/internal endpoints, wrong/foreign cursors, root empty,
  wide 12k-node / 6k-edge case, local HTTP 400/404/405/413,
  tamper-safe public errors and immutable source IDs.
- T119/T012 dependencies remain **partially** implemented and not
  finally accepted. ADR-0018 documents an explicit **local experimental**
  alternative, not approved architecture. No auth/persistence UI
  integration, nor claims of million-edge production capacity.
- Commit, CI and merge outcomes must be appended only after direct
  observation of the published SHA and hosted workflows.
