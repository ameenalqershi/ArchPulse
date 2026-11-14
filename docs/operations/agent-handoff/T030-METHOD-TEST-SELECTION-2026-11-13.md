# S31/T030 — method-test selection isolation (2026-11-13)

- Base: main `2e755869c672caf384c200106534f1e1cecab658`; isolated
  VPS checkout `/tmp/archpulse-agent-batch3-20261113`.
- Branch: `fix/s06-stale-selection-20261113`; proposed PR is **not yet
  merged** and PR/commit identities must be rechecked before resuming.
- Scope: avoid transient display of the old method's test associations after
  the user selects a different method in Web Studio. Keep the existing
  DataJourney selection contract; do not promote static evidence to runtime.
- Mechanism: extract method-test lookup into selection-keyed React component;
  cancel abandoned fetches; show loading until the exact query is resolved.
- Browser regression uses a controlled synthetic sidecar for UI behavior only,
  holds the next response and asserts absence of prior evidence.
- Verification: record exact checkout SHA, locked .NET restore and test counts,
  production Web Studio build, Vitest, real Chromium tests, native validators,
  and hosted CI after publishing the branch. Do **not** count test fixture
  labels as independent proof of actual coverage.
- Nonclaims: S31/S33, runtime observability and hosted Auth are still partial.
- Next: inspect actual published PR and exact GitHub CI; review UX and
  compatibility with #54 before a governed merge.
