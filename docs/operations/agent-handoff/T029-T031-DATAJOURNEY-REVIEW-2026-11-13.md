# DataJourney policy and evidence review handoff

- Tasks: T029, T030, T031. Specification: S06. Status: partial implementation.
- Parent: `b4bb898d90c0911ec018895fe5f36532584c3412` from [PR #51](https://github.com/ameenalqershi/ArchPulse/pull/51).
- Branch: `feat/s06-datajourney-review-t029-t031-20261113`.

## Completed

- Added the local CLI review command with directional classification grants.
- Added an expandable HTML evidence view and a separate aggregate-only summary.
- Added coverage for reverse-direction returns, unmapped zones, malformed
  policy, omitted fixture values, reflection and dynamic SQL analysis gaps.
- Documented evidence boundaries and evaluation metrics in the
  [review guide](../../guides/datajourney-review.md).

## Reproduce

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/datajourney-cross-project/Journey.slnx --locked-mode
dotnet restore examples/datajourney-local/DataJourney.sln --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release
dotnet test ArchPulse.sln --no-build -c Release
bash scripts/ci-datajourney-cross-project.sh
bash scripts/ci-datajourney-review.sh
bash scripts/ci-datajourney-local.sh
```

Reconcile the published commit, request base and exact GitHub check results
before claiming hosted verification. If the stacked parent merges, inspect how
its commits were included and update the review base to main without losing
accepted commits.

VPS verification passed: Release build (zero errors or warnings), 247 of 247
.NET tests and all three DataJourney smokes. Verification log SHA-256:
`1929065645723db94cc03d05d98dba49422e1a23c9954f20c40e4d267c4dd391`.

## Pending

Runtime observation and editor integration remain open. Application policy
enforcement, field-level classification, EF/SQL and message transports,
sanitizer verification, and production precision measurement remain open.
The fixture treats reflection and dynamic SQL construction as uncertainty.
Human architectural and privacy review is pending.

## Next action

Review the published change after all checks finish. Extend S06 with bounded,
source-backed storage and message adapters and independent observed evidence.
