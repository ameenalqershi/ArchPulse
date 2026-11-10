# ArchPulse GitHub configuration evidence — 2026-11-10

**Status: Verified settings / scope-limited execution.** This report records external GitHub checks and the local VPS toolchain. It does not claim ArchPulse's primary features have been implemented or that all security risks were eliminated.

## Repository identity and community

- **Canonical repository:** [ameenalqershi/ArchPulse](https://github.com/ameenalqershi/ArchPulse); `public`; default branch `main`; root LICENSE recognized by GitHub as `Apache-2.0`.
- **Technical topics (10, independently retrieved through GitHub REST):** `ai-agents`, `architecture-as-code`, `csharp`, `developer-tools`, `dotnet`, `mcp`, `open-source`, `opentelemetry`, `roslyn`, `software-architecture`.
- **Public coordination:** [GitHub Project 1](https://github.com/users/ameenalqershi/projects/1), [Gate A–E milestones](https://github.com/ameenalqershi/ArchPulse/milestones), [Issues](https://github.com/ameenalqershi/ArchPulse/issues), [Discussions](https://github.com/ameenalqershi/ArchPulse/discussions) and [Welcome announcement #16](https://github.com/ameenalqershi/ArchPulse/discussions/16). Added specialized categories for architecture RFCs, contributor introductions, and mentoring.
- **Public first-contribution issues:** [#17](https://github.com/ameenalqershi/ArchPulse/issues/17) (clean .NET setup verification) and [#18](https://github.com/ameenalqershi/ArchPulse/issues/18) (bilingual docs/accessibility).
- **Role model:** one authorized project owner; contributor PRs do not require membership and no unconsenting collaborator has been invited.

## Branch rules and CI

- **Ruleset:** [Protect main](https://github.com/ameenalqershi/ArchPulse/rules/26926696), active, targeting `main` (GitHub REST `GET /repos/ameenalqershi/ArchPulse/rules/branches/main`).
- Enforced rules include `pull_request`, `required_status_checks`, `deletion` and `non_fast_forward`.
- Required checks are `foundation-build-test` and `foundation-document-check`, both shown as **success** for [`81ca1f841e6dbf2deffec28d2d3875aa85d1a900`](https://github.com/ameenalqershi/ArchPulse/commit/81ca1f841e6dbf2deffec28d2d3875aa85d1a900), from [GitHub Actions run #42787649387](https://github.com/ameenalqershi/ArchPulse/actions/runs/42787649387).
- This commit's check-runs also showed successful `Analyze (actions)`, `Analyze (csharp)` and `Analyze (python)` CodeQL jobs. **CodeQL default setup was enabled**; do not add conflicting advanced setup. Ongoing scan success must be rechecked per revision.
- One-owner bootstrap review count is **zero**; independence of human code review has **not** been established. Do not misreport this as enforced human approval.

## GitHub security

Verified in the authenticated GitHub Advanced Security settings: private vulnerability reporting **enabled**; dependency graph **on**; Dependabot alerts **on**; Dependabot security updates **on**; push protection **on**; code security CodeQL default setup configured. Dependabot version PRs exist and must be independently reviewed before merging.

**Important limitations:** This does not prove a complete dependency-license audit, closed vulnerabilities, production security posture, or verified sandbox boundaries. The public GitHub REST `code-scanning/analyses` and Dependabot alert endpoints require authenticated access and returned 401 from the read-only VPS; do not interpret that response as zero alerts. See [SECURITY.md](../../SECURITY.md).

## Developer verification and remaining work

- Authorized VPS: `/opt/archpulse`, .NET SDK 10.0.114, Git, Python 3. The local checkout was fast-forwarded to the GitHub-published `81ca1f8` revision.
- Structural validation passed. Traceability validator confirmed **34 proposed specs, 205 planned tasks, 192 planned requirements**; Python contract suite passed **16/16** and .NET Release unit tests previously **4/4** (verify exact output again for future revisions).
- **Not completed:** full product functionality, independent second-agent handoff rehearsal, a comprehensive third-party license audit, mature independent reviewer coverage, official trademark clearance and complete production deployment verification.
- Check remote commit, current security settings, CI and open PRs again before editing or merging; this is a historical snapshot, not live telemetry.

## Evidence sources

[GitHub repository API](https://api.github.com/repos/ameenalqershi/ArchPulse), [branch rules API](https://api.github.com/repos/ameenalqershi/ArchPulse/rules/branches/main), [actions](https://github.com/ameenalqershi/ArchPulse/actions), [public Project](https://github.com/users/ameenalqershi/projects/1), [Notion specifications index](../requirements/specification-index.md) and [Task registry](../requirements/task-registry.md).
