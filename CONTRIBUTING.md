# Contributing to ArchPulse

We welcome fixes, docs, tests, diagrams, Roslyn fixtures, security design, MCP interoperability and architectural research. **Contributors do not need repository membership** to fork or submit pull requests. Work must not claim an unimplemented feature exists.

1. Read [README](README.md), [Architecture](docs/architecture/README.md), [Security](SECURITY.md), [Roadmap](ROADMAP.md), and task specification in Notion.
2. Select an unassigned issue with `help wanted` or `good first issue`; ask on the issue before undertaking a large cross-module change. Do not assume a Notion task has a GitHub issue.
3. For behavior-changing architectural work, file an architecture proposal/RFC with **Mermaid diagrams for every alternative**, constraints, human decision, and linked spec; do not silently change accepted contracts.
4. Install free .NET 10 SDK and a Git client. No AI account or API subscription is required for foundational development.
5. Fork, clone and branch from `main`: `docs/Txxx-short-topic`, `feat/Txxx-short-topic`, `fix/Txxx-short-topic`, or `test/Txxx-short-topic`.
6. Work in small reviewed increments. Use conventional-ish subjects such as `docs(T001): map evidence states` or `test(T089): cover invalid receipt`. Avoid misrepresenting implementation status.
7. `dotnet restore ArchPulse.sln`, `dotnet build ArchPulse.sln --no-restore -c Release`, `dotnet test ArchPulse.sln --no-build -c Release`. Include tests for changed behavior, a failing-case reproduction for bugs, and docs/ADR updates for contract changes.
8. Never paste credentials, personal data, unredacted trace values, or logs with source secrets into issues or pull requests. Do not execute untrusted repository tests outside an approved sandbox.
9. Open a Pull Request using the template. Include Txxx / Sxx / R IDs; rationale; alternatives; evidence/source attribution; files; tests and exact commands/results; migration risk; UI screenshots if appropriate; security and privacy impact.
10. Respond to reviewers, keep changes scoped and CI green. Passing CI is necessary, not proof of every product claim. Maintainers decide merging; no automatic promotion or administration rights.

## Review criteria

Correctness, test determinism, module boundaries, public API compatibility, predictable resource usage, proof of evidence labels, secrets/PII protection, accessibility, complete docs, licensing of new dependencies, and reversible migration. Unverifiable claims remain `Unknown` or `Proposed`.

## Legal provenance

The project uses Apache-2.0. Contributions are submitted under the repository's license unless expressly agreed otherwise. No contributor CLA or DCO sign-off is required by this foundation. If the governance body adopts a DCO later, each human signs for themselves; bots must not forge signatures. See [third-party policy](docs/operations/third-party-policy.md).

## Help and mentoring

Use Discussions for general questions (after the category is configured) and issue comments for scoped implementation questions. Response targets, not guarantees: first triage within 7 days and PR feedback within 14 days, depending on actual maintainer availability. See [Support](SUPPORT.md).