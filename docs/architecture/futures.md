# Futures Engine & AET

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S12. **Tasks:** T070–T079. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
Represent architectural experiments as durable transactions that branch candidates, run fair workloads, compare measurements and present a human decision.

## Boundaries / safety
Never equate a proposal with a proven solution; preserve origin commit, instrument costs and human sign-off.

## Acceptance evidence
State-machine integrity, matching workloads, fail/recover semantics and independent experiment reproduction. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
