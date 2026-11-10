# Roslyn Semantic Analysis

**Design status:** Proposed; not implemented in this repository foundation. **Specifications:** S03. **Tasks:** T013–T017. The [Notion specification index](../requirements/specification-index.md) is authoritative.

## Target behavior
Load .sln/.csproj using MSBuildWorkspace; track C# symbols, calls, references, async transitions and DI heuristics.

## Evidence, boundaries and safety
Code-derived structure is Static, never observed execution; analyzers must be incremental and cancelable.

## Verification required before claiming completion
Fixture tests across namespaces, generics, partial classes, and multi-target compilation. Link tests to exact commit/build hashes, real logs, and a maintainer-reviewed PR. No projected or inferred statement is classified as Verified by the documentation alone.

## Integration contract
Do not bypass explicit module boundaries or mutate the canonical evidence/event ledger from untrusted agent output. New public contracts require versioning, security review and an ADR with alternatives where architecture changes.
