# Foundation verification ledger — 2026-11-10

This is an evidence ledger for **repository foundation only**, not a product readiness report. Verify remote state on every handoff; historical evidence may be stale.

| Item | Classification | Evidence and limits |
| --- | --- | --- |
| Repository identity and visibility | Verified | `ameenalqershi/ArchPulse`, public, default branch `main`; repository ID 1452203231, GitHub repository API |
| License recognition | Verified | GitHub repository API reports `spdx_id: Apache-2.0`; root `LICENSE` contains the full official Apache License v2.0 |
| Notion planning registry | Verified as **design inventory** | 34 proposed specifications; 205 tasks, 0 Verified at query time; 192 requirement rows in committed snapshot. No product feature is verified by these counts |
| Build environment | Verified locally | Authorized Linux VPS, .NET SDK 10.0.114. Local workspace `/opt/archpulse-work` |
| Restore | Verified locally | `dotnet restore ArchPulse.sln --locked-mode`, completed successfully on base revision `5a2f47019b509512819fa2f93202726d6c43f805` |
| Build | Verified locally | `dotnet build ArchPulse.sln -c Release --no-restore`: zero errors, zero warnings |
| Tests | Verified locally | `dotnet test ArchPulse.sln -c Release --no-build --no-restore`: 4 passed / 0 failed / 0 skipped |
| Structural validator | Verified locally | `python3 scripts/validate_foundation.py` exited successfully; structural checks do not prove features |
| GitHub Actions | Verified on workflow run | [Run 42758731375](https://github.com/ameenalqershi/ArchPulse/actions/runs/42758731375): `foundation-build-test` and `foundation-document-check` both completed successfully on `3c694ac44f445fba60c9e892e76ca96eb3022725`. This is **not** a successful run on the current `main` SHA |
| Branch ruleset | Verified | [Ruleset 26926696](https://github.com/ameenalqershi/ArchPulse/rules/26926696) active: PR required, two foundation checks, branch deletion and non-fast-forward prohibited; zero required approvals while one-owner |
| Other GitHub settings | Partially verified | Issues, Discussions and Projects repository flags enabled; actual Discussion categories, milestones, Project board and topic tags not confirmed |
| Security scans | Unknown | Dependabot configured; successful Dependabot runs are **not** CodeQL or security audit findings. Advanced security, secret-scanning and dependency alert settings were not confirmed |
| Community membership | Planned | Governance documented; no person is assigned a role without consent. No evidence of mentor/reviewer acceptance |
| Full feature acceptance | Planned | Knowledge Graph, Guardian, Futures, MCP Apps, test journey and UI are NOT implemented by the foundation |
| Handoff | Partially verified | Existing checkpoint `FOUNDATION-2026-11-10.json` is historical and contains outdated pending actions; see new audit checkpoint. Independent agent B rehearsal is still not proven |

## Evidence boundaries

- The previous checkpoint recorded publication, CI and protections as pending. These are now independently confirmed in GitHub, but only within the scopes above.
- GitHub App branch protection GET returned HTTP 403, while repository Rulesets GET succeeded and showed the active rules explicitly.
- `gh` on the VPS is not authenticated; do not assume the machine can push to GitHub. Use the authorized GitHub connector for remote changes.
- Do not publish raw Notion contents without privacy review. Notion remains the planning source; GitHub is the execution source.

## Revalidation commands

```bash
git status --short --branch
git rev-parse HEAD
python3 scripts/validate_foundation.py
python3 -m unittest discover -s scripts -p 'test_*.py' -v
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build --no-restore
```

The latest verified GitHub branch/PR and CI receipt must be added to a new checkpoint after publication; never silently re-label an unverified external effect as success.
