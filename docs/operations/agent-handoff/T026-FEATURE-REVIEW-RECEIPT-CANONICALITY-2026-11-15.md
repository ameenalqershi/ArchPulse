# T026/S05 — Feature Review JSON receipt integrity (2026-11-15)

## Context and isolation

- Base `main` SHA: `514d3fad1cb2bd6198a769888893505031b27fd5`.
- Independent VPS clone and feature branch: no shared worktree, `main`, or other active PR branch changes.
- Scope: existing local-only, operator-declared `FeatureReviewJournal` read path and request parser; no HTTP/MCP write path or external service.

## Reproduced failures and fix

- Five xUnit cases failed before fix: a receipt with exact or case-aliased duplicate action field, an unrecognized hidden JSON claim, or a local request with conflicting exact/case-aliased action keys was silently accepted.
- Persisted receipts now require byte-for-byte equality against the canonical serializer representation before the existing SHA-256 model hash and scope/sequence checks.
- Local request parser now rejects exact/case-insensitive duplicate property names recursively; does not require canonical whitespace, and normal behavior remains unchanged.
- Exact output is an intentionally strict on-disk v1 contract. Do not claim independent cryptographic attestation, human identity proof, complete business verification, durability against privileged local writers, or fully implemented S05.

## Verification and next safe action

- Local red/green: five regression failures reproduced, then 14/14 FeatureReviewJournal xUnit tests pass.
- Before merge: locked restore, Release build, full .NET tests, RepositoryValidator and TraceabilityValidator, scoped whitespace check, clean published SHA re-clone, and hosted CI on exact PR SHA.
- T026 remains In Progress, S05 Proposed; human review and approved dependencies required before acceptance.
