# GitHub repository operations — current foundation

Repository: [ameenalqershi/ArchPulse](https://github.com/ameenalqershi/ArchPulse) is **Public** on `main`, with GitHub-recognized **Apache-2.0**.

## Verified settings (2026-11-10)

- Issues and Discussions enabled; Wiki deliberately disabled to avoid duplicate documentation.
- GitHub Projects: [public board](https://github.com/users/ameenalqershi/projects/1), alongside five [Gate A–E milestones](https://github.com/ameenalqershi/ArchPulse/milestones). There are open tasks with explicit Txxx/Sxx dependency links, plus scoped `good first issue` tasks; opening an issue is not completion.
- [Active ruleset](https://github.com/ameenalqershi/ArchPulse/settings/rules): PR required for `main`, existing passing checks `foundation-build-test` and `foundation-document-check`, deletion and non-fast-forward pushes prohibited. Required approval count remains zero during one-owner bootstrap; an independent human reviewer has **not** been appointed.
- Private vulnerability reporting, Dependency Graph, Dependabot alerts and security updates, GitHub secret/push protection enabled. CodeQL is configured using **default setup** (do not also add duplicate advanced setup).
- [Foundation CI](https://github.com/ameenalqershi/ArchPulse/actions) and CodeQL checks have succeeded on the exact `main` SHA listed in the dated evidence snapshot.

See [dated verification details](GITHUB-STATE-2026-11-10.md) and the [project audit ledger](FOUNDATION-VERIFICATION.md). Recheck GitHub whenever taking a new administrative action.

## Maintainer procedure

1. Verify repository identity, real account permissions, remote `main` SHA, current checks and open PRs before changes.
2. Open a branch and PR for content changes; never force-push or remove history. Do not disable security settings as a shortcut.
3. Avoid adding mandatory status contexts before a successful real run demonstrates the exact names.
4. Maintain the complete final product scope from Notion, but never present architecture diagrams or planned components as implemented.
5. Prefer `good first issue`, scoped mentoring, and documented review for new contributors. Membership is optional and grants no automatic write/admin powers.
6. Review third-party license evidence before NOTICE decisions, and document unresolved name/trademark concerns.
7. Preserve a versioned checkpoint after externally visible actions; investigate Unknown outcomes rather than blindly retrying.
