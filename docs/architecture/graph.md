# Engineering Knowledge Graph & Provenance

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S02. **Tasks:** T009–T012. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Repository/Solution/Project/Module/Type/Method/Feature/Trace/Test nodes; versioned evidence-derived graph; stable identities, causal and dependency edges.

## Evidence, boundaries and safety
Never elevate AI hypotheses to facts; include source URI, build SHA, capture time and evidence class; allow Unknown.

## Verification required before claiming completion
Event projection rebuild, identity stability, provenance isolation, stale-revision detection. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
