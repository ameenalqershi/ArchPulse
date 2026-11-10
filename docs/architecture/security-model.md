# Security, Privacy and Sandbox

**Design status:** Proposed; **not implemented** in the repository foundation. **Specifications:** S13,S16. **Tasks:** T080–T088,T191,T203. Consult the [source specification index](../requirements/specification-index.md) and the [Notion work registry](../requirements/task-registry.md).

## Target behavior
Treat repositories, CI scripts, AI output, plugins, traces and remote execution as untrusted; isolate code execution from control plane and secrets.

## Boundaries / safety
Least privilege, RBAC/ABAC, PII/secret redaction, SSRF/path traversal defenses, audit event retention and explicit deletion.

## Acceptance evidence
Threat modeling, dependency audits, adversarial tests and production security sign-off. Do not upgrade the task status until an actual commit, reproduced tests and maintainers' review support that transition.

## Design governance
Breaking API, public protocol or cross-module decisions need documented alternatives (including valid Mermaid for each) and human architectural approval when required.
