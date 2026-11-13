# FeatureAtlas & DataJourney

**Design status:** S05 and S06 remain Proposed; full capabilities are not implemented. **Specifications:** S05,S06. **Tasks:** T023–T031. The [Notion specification index](../requirements/specification-index.md) is authoritative.

**Implemented T023 experimental slice (2026-11-13):** a native C# operator-authored, immutable feature overlay over exact-version GraphSnapshot nodes. The read-only CLI, local Graph API and MCP tool project only explicitly selected symbols and existing static graph edges. Human membership links are always `Designed`/`Inferred` candidates, never automatically `Verified` or `Observed`. See [FeatureAtlas local guide](../guides/feature-atlas-local.md). **T024 has an additional partial source-signal implementation:** exact-revision Roslyn MVC controller-action metadata, named Handler methods and test declarations as bounded **Inferred candidate hints**, queryable from local API/MCP. There is no automatic BusinessFeature membership or registered-route/observed-test claim; see [feature discovery guide](../guides/feature-discovery-local.md). **T025 now has a partial static structural impact projector:** two exact Graph revisions and a human candidate feature catalog yield direct-node and source-call-path impact hints, never a full Git PR or observed runtime impact ([guide](../guides/feature-impact-local.md)). **T026 has an additional partial local operator-declared immutable review journal**: explicit human-labeled candidate decisions, SHA-256 receipt chaining, optimistic head checks and additive rollback, without authentication or business verification ([guide](../guides/feature-review-local.md)). Full T024, full T025, authenticated/interactive T026 and DataJourney T027–T031 remain outstanding.

## Target behavior
Map business capabilities through endpoints/services/domain/persistence/tests and static/observed data paths.

## Evidence, boundaries and safety
Separate value propagation from control flow, label private sources and sinks, redact sensitive payloads.

## Verification required before claiming completion
Feature ownership, false-positive audit, privacy and source-to-sink contract tests. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
