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

## Experimental canonical-ownership dependency evaluation — T019/S04 partial (2026-11-15)

The current limited forbidden-dependency evaluator determines each source and destination
symbol's project from the graph's validated canonical ParentSymbolId chain, **not**
a string prefix or any spelling convention in SymbolId. This means arbitrary stable
symbol identities and nested namespace/class/method ownership are supported without
misattributing a call to a similarly named project. Repeated project labels make a
name-scoped policy ambiguous; the result is NotEvaluated, never a proven violation.
Missing scopes and absent static edges also remain NotEvaluated, never Pass.
Only Static/Observed/Verified evidence can yield a violation in the current
bounded rule; Inferred/Designed remain Unknown.

The owner index is built once per evaluation in O(nodes), without recursion.
This is a source-backed local prototype, **not** a complete ArchitectureGuardian:
approved policy authoring, authentication of waivers, drift/revision contracts,
witness-path composition, security/CI enforcement and visual governance remain
unimplemented or not accepted. S04 remains Proposed and T019 In Progress.
