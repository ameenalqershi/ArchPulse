# FeatureAtlas & DataJourney

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S05,S06. **Tasks:** T023–T031. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Map business capabilities through endpoints/services/domain/persistence/tests and static/observed data paths.

## Evidence, boundaries and safety
Separate value propagation from control flow, label private sources and sinks, redact sensitive payloads.

## Verification required before claiming completion
Feature ownership, false-positive audit, privacy and source-to-sink contract tests. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
