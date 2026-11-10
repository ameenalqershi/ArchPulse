# ArchitectureGuardian: Enforceable Contracts

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S04,S28. **Tasks:** T018–T022,T141–T150. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Policy-as-code for dependencies, cycles, layers, API contracts, ownership and waivers; source-backed violations with visual annotations.

## Evidence, boundaries and safety
Never silently alter accepted architecture; policy exceptions need human review and durable receipts.

## Verification required before claiming completion
Rule fixture, explainable witness path, preflight/post-edit consistency and VG-01..VG-10. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
