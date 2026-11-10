# Security policy

## Current support scope

ArchPulse is **under active development**; no production release has been declared. Security reports about the published repository foundation, CI supply chain, dependencies, documentation, credentials and future design/implementation are welcome. A proposed threat model is not an assertion that a product security capability has shipped.

## Privately reporting a vulnerability

**Use [GitHub Private Vulnerability Reporting](https://github.com/ameenalqershi/ArchPulse/security/advisories/new)** for this repository. This channel was enabled and verified in the repository's Advanced Security settings on 2026-11-10. GitHub may require that a reporter be signed in.

- Describe affected revision(s), reproduction steps, actual impact and a minimal safe proof where available.
- Do **not** post secrets, exploit details, personal information or unpublished vulnerabilities in public Issues, Discussions, PRs or agent checkpoints.
- Do not test third-party systems without their authorization. Never exploit production systems as part of this project.

Maintainers aim to acknowledge actionable reports promptly and will coordinate impact assessment, remediation, regressions and disclosure when appropriate; **no fixed SLA or bounty is promised**. If the GitHub private form is unavailable, request a private contact route from the verified repository owner without disclosing sensitive details publicly.

## Security design and operational protections

- The repository currently has GitHub secret protection/push protection, a dependency graph, Dependabot security alerts/updates and CodeQL default scanning configured. Exact verification status and limits are recorded in [the repository settings evidence snapshot](docs/operations/GITHUB-STATE-2026-11-10.md).
- PRs targeting `main` require the existing foundation CI checks, and deletion/force-push are blocked. This does **not** establish that all future source code is safe.
- Future agent runtimes, sandbox workers, MCP calls and traces must respect least privilege, explicit human approval for protected actions, source-origin checks, privacy redaction, bounded resource usage and reproducible evidence.
- AI-produced claims must not be automatically accepted as security findings, authorizations or test results.

See [S16 security model](docs/architecture/security-model.md) and [Third-party policy](docs/operations/third-party-policy.md). Those documents describe **planned** capabilities and engineering obligations, not an already complete production platform.
