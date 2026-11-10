# Public APIs, Extensions and Client Surfaces

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S14,S23,S24. **Tasks:** T006–T008,T044–T052. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
Versioned ASP.NET Core API and event stream, Web Studio, VS Code adapter, CLI/SDK and pluggable language/telemetry/agent providers.

## Boundaries / safety
Each client enforces scopes; extensions are untrusted until signed/verified and run with explicit capabilities.

## Acceptance evidence
OpenAPI/MCP contract tests, client version compatibility, restricted workspace trust and remote authorization. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
