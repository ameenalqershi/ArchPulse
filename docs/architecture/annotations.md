# Engineering Annotation Engine

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S02,S28. **Tasks:** T146–T148. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Propose contextual purpose and ownership annotations from XML docs, code comments, ADRs, tests and OpenAPI.

## Evidence, boundaries and safety
Annotations are untrusted claims unless independently supported; conflicts and drift are explicit.

## Verification required before claiming completion
Provenance and change detection, non-leakage of confidential documentation. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
