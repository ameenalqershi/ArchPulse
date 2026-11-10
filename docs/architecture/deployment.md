# Deployment Topologies and Operations

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S18. **Tasks:** T094–T116. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
Support local workstation with SQLite and loopback, authenticated Linux/VPS, self-hosted team workers/PostgreSQL, optional managed deployment.

## Boundaries / safety
No public unauthenticated control plane; sandbox workers separated; secret injection at runtime, not committed.

## Acceptance evidence
Recovery drills, backup/restore, observability and rollback tests. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
