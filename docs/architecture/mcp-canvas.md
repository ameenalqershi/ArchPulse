# MCP Interactive Engineering Decisions and Bidirectional Canvas

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S10,S29,S30,S32. **Tasks:** T151–T169,T180–T185. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
MCP tools/resources and optional MCP Apps display contextual engineering alternatives; selection references move between chat and canvas; each alternative provides valid Mermaid.

## Boundaries / safety
Human approvals are durable server records with scope, revision, identity and explicit authorization; unsupported hosts use read-only Mermaid/source fallback.

## Acceptance evidence
DEC-01..DEC-12, BC-01..BC-10 and MD-01..MD-10, replay and prompt-injection defense. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
