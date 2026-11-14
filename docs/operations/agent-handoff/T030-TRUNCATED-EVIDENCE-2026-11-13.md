# T030 — evidence selection under truncated DataJourney analysis

- Date: 2026-11-13.
- Specs: S06 / S09; component: revision-scoped static DataJourney method selection.
- Base: `main` at `2e755869c672caf384c200106534f1e1cecab658`.
- Implementation branch: `fix/s06-truncated-evidence-status-20261113`.
- Status: bounded corrective PR; **T030 and S06 remain partial**. ADR-0014 remains Proposed; no human architectural approval is implied.

## Behavior and rationale

An indexed report may be truncated at an analysis budget, retaining witnesses
for some methods but not others. The old endpoint reported `NoWitness` for
every unmatched method, even if the report did not cover all paths. That
confused *no retained witness* with *exhaustive absence*.

The read-only API now reports `Indeterminate` only when no selected-method
finding exists **and** `report.Truncated` is true. `NotIndexed`,
`NoWitness` (non-truncated), and `Mapped` retain their meaning. The
browser validates this conditional contract and displays explicit uncertainty.
The runtime status remains `Unknown`, even with static witnesses. No data
payloads, source values, or additional execution permissions are introduced.

## Verification on isolated Linux VPS checkout

- `dotnet restore ArchPulse.sln --locked-mode` — passed.
- `dotnet build ArchPulse.sln -c Release --no-restore` — passed, 0 warnings, 0 errors.
- `dotnet test ArchPulse.sln -c Release --no-build` — passed, 251/251.
- `npm ci --prefix apps/web-studio` — passed.
- `npm run build --prefix apps/web-studio` — passed.
- `npm run test --prefix apps/web-studio` — passed, 13/13.
- `dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root . --handoffs` — passed.
- `dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root .` — passed.
- `dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build -- --root . --baseline` — passed.
- `bash scripts/ci-datajourney-studio.sh` — passed, 1/1 real Chromium graph navigation check.
- Run locations are agent-local scratch; GitHub CI on exact PR commit must still be checked.

## Acceptance boundaries and handoff

The new `Indeterminate` response value extends the experimental v1 selection
status vocabulary and deserves client compatibility / human architectural review.
The non-truncated `NoWitness` state is still **not** a proof of safety because
the supported analyzer is a partial static slice. EF Core/SQL and broker lineage,
runtime correlation, production hardening and privacy-policy enforcement remain
out of scope. Do not mark them as complete.

Next: inspect the published PR and its exact SHA, wait for hosted required
checks, and obtain human contract review before merging. Never reinterpret
Missing/Unknown runtime evidence as a negative execution result.
