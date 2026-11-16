# T121 local canonical expansion API handoff

- Isolated checkout: /tmp/archpulse-t121-local-expansion-api-20261116
- Base main SHA: a3a1dcec62140f8a1a0074bd1241312fba6ffd7d
- New local GET `/api/graphs/{project}/{revision}/local-scope-expansion` with explicit scopeSymbolId and optional lens/maxNodes/maxEdges/depth parameters, no write endpoint. It uses existing immutable local ViewScope descriptor and bounded canonical expansion. HTTP tests assert identity/evidence/partial, input bounds, revision, missing scope and POST 405.
- Local xUnit solution 354/354 PASS. ADR validator and Traceability baseline PASS. Not a real permission-bearing API; server is loopback-only and AccessStatus=AuthorizationNotEvaluated. Query loads entire graph snapshot and has no continuation, no signed cursor, no production scalability guarantees. S27 and T121 remain incomplete and Proposed where applicable.
- Validate hosted CI and code formatting on exact commit before merge; preserve all other agents' changes.
