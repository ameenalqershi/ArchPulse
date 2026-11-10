# OpenRepoLab & In-Place Test Mapping

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S07,S31. **Tasks:** T064–T069,T170–T179. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Discover test frameworks and associate tests with methods without rendering test cases as standalone graph blocks by default; scoped sandboxed execution.

## Evidence, boundaries and safety
Link confidence and run freshness must be visible; never execute untrusted code in the control process.

## Verification required before claiming completion
VSTest/MTP and xUnit/NUnit/MSTest adapters; IT-01..IT-12 and reproducibility. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
