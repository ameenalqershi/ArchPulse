# T106/S19 — non-privileged .NET bootstrap evidence (2026-11-14)

**Scope:** a narrow documentation correction supporting GitHub issue #17. This is *not* full T106 completion; dependencies T105 and T096 and an independent external contributor remain outstanding.

## Source and reproduction

- GitHub public upstream: `ameenalqershi/ArchPulse`
- Exact tested revision: `2e755869c672caf384c200106534f1e1cecab658` (main)
- Isolated fresh checkout of the public HTTPS URL, with Git credential helpers disabled and `GIT_TERMINAL_PROMPT=0`; tested under unprivileged Linux account `nobody` / `nogroup`. No authenticated repository write access was used in the reproduction.
- Environment: Ubuntu 24.04; .NET SDK 10.0.114; Git 2.43.0; separate writable home and initially empty NuGet package cache. This was a Linux VPS account-separation rehearsal, not an independently owned contributor machine or a GitHub fork.
- No paid model/provider credentials were necessary. The initial NuGet restore did access package sources.

| Command, from public checkout root | Observed exit | Observation |
| --- | --- | --- |
| `git -c credential.helper= clone --depth=1 https://github.com/ameenalqershi/ArchPulse.git source` | 0 | checked out exact main SHA |
| `dotnet restore ArchPulse.sln --locked-mode` | 0 | restore succeeded |
| `dotnet build ArchPulse.sln -c Release --no-restore` | 0 | zero errors and zero warnings |
| `dotnet test ArchPulse.sln -c Release --no-build` | 0 | **250 passed**, zero failed, across 8 test assemblies |
| `dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root .` | 0 | `FOUNDATION STRUCTURE VALIDATED — NOT proof of product functionality` |
| `git status --porcelain` | 0 | empty after reproduction |

## Documentation correction and security review

- Developer guide now uses a locked restore and explicit `--no-build -- --root .` validator invocation. It distinguishes public clone vs. fork and local tests vs. Web Studio, provides SDK/NuGet troubleshooting without administrator privileges, and prohibits publishing credentials/environment dumps.
- No source, package, network endpoint, permission, credential, dependency, workflow, or public API contract changed. Documentation-only branch from the tested `main`.
- The first-contribution walkthrough is concurrently edited in open PR #56 and was deliberately left untouched here to minimize merge conflicts.
- The reproduction proves that **one controlled non-privileged account** can follow the current .NET commands; it does not prove every OS, offline restore, an external contributor's GitHub fork, security hardening, full T106 acceptance, or product readiness.

## Independent review and handoff

- A reviewer should inspect the exact Pull Request head, hosted CI and the corrected guide. A genuine new contributor should reproduce on a separate machine and report redacted failures.
- Notion T106/S19 remained read-only, with no status mutation. Do not mark T106 or S19 Verified/Approved.
- Reconcile any subsequent changes with GitHub main and open PRs before applying further documentation changes.
