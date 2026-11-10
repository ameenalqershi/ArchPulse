# Hierarchical Diagram Navigation

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S27. **Tasks:** T119–T140. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Master system map, nested module/service/class/method scopes, breadcrumb, semantic zoom, cross-scope portals, call/control/data/runtime lenses.

## Evidence, boundaries and safety
Stable node IDs and bounded graph queries; no false edge aggregation; permissions enforced at every scope.

## Verification required before claiming completion
AV-01..AV-14, large-repository layout and accessible tree fallback. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
