# Txxx / Sxx workflow

1. Find the exact Txxx in [task-registry](task-registry.md), and the related Sxx and R-xxx in [specification-index](specification-index.md) and [traceability](traceability.md).
2. Validate Notion status and dependencies. Discuss blocked or architecture-changing prerequisites before coding.
3. Create a GitHub issue with clear acceptance and verification steps. Do not create duplicates.
4. For cross-module contracts, present an ADR with at least two evidence-aware Mermaid alternatives and obtain human approval where required.
5. Implement in a branch, test, document and create a reviewable PR. Record actual CI and revision.
6. Notion remains Proposed/Planned until code, tests and verification evidence justify an approved transition.
7. Update agent handoff checkpoint for recovery.
