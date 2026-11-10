# Visual Architecture Governance

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S28,S22. **Tasks:** T141–T150. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Display architectural expected paths versus code-derived paths and deviations in hierarchical diagrams.

## Evidence, boundaries and safety
Every warning preserves rule ID, source revision, severity, confidence and proposal status.

## Verification required before claiming completion
Cross-scope violation propagation and human approval invalidation tests. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
