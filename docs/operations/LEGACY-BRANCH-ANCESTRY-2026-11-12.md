# ArchPulse — historical branch ancestry reconciliation (2026-11-12)

**Status:** provenance reconciliation of pre-rebuild Git history, **not** delivery of additional product capabilities. This is a deliberate merge with superseded file content **excluded** when it would regress the native C#/.NET platform. It does not certify a full security audit or complete acceptance of the historical agent work.

## Reviewed revisions

| Purpose | Branch | Exact tip reviewed |
| --- | --- | --- |
| Existing validated implementation | `main` | `8bc6d81bacd4f8af223c26243e5f646c7410454b` |
| Security/community setup evidence | `chore/community-security-evidence-20261110` | `0ddf9e7a6e51549acb9c3deecc1c854ee4d244fd` |
| Foundation evidence and original Python validators | `chore/foundation-evidence-audit-20261110` | `646b2e972b73c3e9151e1141114eded461e9ea8b` |

The authorized owner requested consolidation of all historic branches. The original tips are retained as additional Git commit parents through two ancestry merge commits on this PR. **No force push, branch deletion, or protected-main rewrite is part of this procedure.**

## Content review and merge policy

### Community/security evidence branch

`git merge-tree --write-tree origin/main origin/chore/community-security-evidence-20261110` returned **exactly** the current main tree `fc967d0d5993dc71f701af1ad3992e79aeda72db`. A local `git merge --no-commit --no-ff` likewise produced **no staged changes**. The prior security, support and dated repository settings information was already present; the historical commit itself was merely absent from main ancestry. The reconciliation therefore records its provenance without duplicating or overwriting those documents.

### Foundation evidence audit branch

A normal three-way merge yielded content conflicts in `.github/workflows/ci.yml`, `AGENTS.md`, `docs/operations/CI-GATES.md`, and `docs/operations/FOUNDATION-VERIFICATION.md`. The reviewed old branch also contained `scripts/validate_traceability.py`, `scripts/test_traceability_validator.py`, and `scripts/test_foundation_contracts.py`: historical Python-specific foundation checks deliberately retired or replaced in the later native .NET rebuild. Reintroducing those old CI and validators would contradict the verified native architecture.

Checks on the five historical documentation artifacts below found byte-identical copies already present on main:
- `docs/contributing/maintainer-nomination.md`
- `docs/contributing/reviewer-onboarding.md`
- `docs/operations/BRANDING-RISK.md`
- `docs/operations/agent-handoff/FOUNDATION-AUDIT-2026-11-10.json`
- `docs/operations/agent-handoff/FOUNDATION-PR15-2026-11-10.json`

`README.md` and `ROADMAP.md` also matched main in the inspected tree comparison. Current .NET CI, security, license-audit gates, and handoff rules take precedence over older branch versions. The historic source remains accessible through Git ancestry; its implementation is **not activated** by this reconciliation.

## What the commit changes

Only this review record is introduced relative to the previously validated main tree. Git merge ancestry makes both historical tips reachable without resurrecting old Python scripts, replacing CI, or falsely reporting that superseded historical changes were applied verbatim.

The reconciliation is subject to the existing PR protection checks and independent hosted Foundation CI/ADR checks. Once merged, verify every remaining GitHub remote branch tip is an ancestor of main. Continue separate explicit review of any old Python negative tests that lack demonstrated C# parity: recording ancestry is not a substitute for functional parity.

## Caveats

- This does **not** establish independent human review, full project completion, security approval, or a production deployment.
- Dated GitHub settings in the old snapshot are evidence about 2026-11-10, not guarantees of current settings.
- There is no authentication or sandbox change, nor any public exposure of experimental MCP or Graph API services.
- No original Git branches are deleted here; they remain available for audit.
