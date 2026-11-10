# Cross-Agent Continuity and Safe Recovery

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S11,S25. **Tasks:** T053–T063,T117–T118. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
Persist task envelopes, immutable checkpoints, leases with fencing, idempotency, external-effect receipts and next-safe-action.

## Boundaries / safety
A crash with unknown external outcome cannot be retried blindly; record Unknown and reconcile.

## Acceptance evidence
Chaos/crash, lost reply, split-brain, lease expiry and cross-provider resume tests. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
