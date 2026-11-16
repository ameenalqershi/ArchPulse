# T121 experimental exact-scope child cursor handoff

- Base: bb4331bb2f370a06a663ae14c6007267945381ad
- Independent branch: feat/t121-exact-scope-continuation-20261116
- Added `ExactScopeChildCursor` with revision/parent/project/page-size/last-child binding; no API integration, no actual continuation page query.
- New tests verify exact binding, rejection of tampered/noncanonical/oversized cursor and bounds.
- Entire local .NET solution: 353/353 PASS, ADR/traceability validators, whitespace and git diff check PASS.
- SHA256 digest is **not authenticated**: never expose as permission-bearing, public trust, or signed token. T047/real authorization is absent.
- T121 remains In Progress. ADR-0021 and S27 Proposed. Only merge after hosted CI; avoid collisions with other agents.
