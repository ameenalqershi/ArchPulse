# Live Runtime Observability

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S08. **Tasks:** T033–T037. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
Consume OpenTelemetry traces/logs/metrics, correlate to deployment/build SHA and static symbols, display scoped overlays.

## Boundaries / safety
Runtime observations are captured evidence for a particular revision; not universal behavior.

## Acceptance evidence
OpenTelemetry compatibility, sampling, reconnect, cardinality and latency tests. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
