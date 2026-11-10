# Test Execution Journey & Visual Replay

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S33,S31. **Tasks:** T186–T205. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
Start at a method test badge, execute a selected test in an isolated runner, correlate nested calls/returns/async/exception/assertions, and replay an evidence-backed journey on the existing graph.

## Boundaries / safety
Instrumentations are opt-in, safe redaction mandatory, partial spans must appear as gaps rather than invented calls.

## Acceptance evidence
TJ-01..TJ-18, runner compatibility, concurrency, test-to-commit correlation and privacy. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
