# S26 requirement baseline lock — reviewer contract

**Status: proposed design index, not implementation evidence.** See [Notion S26](https://app.notion.com/p/4143830ba34d8185a0edc83f974c55cd), [T001](https://app.notion.com/p/4143830ba34d817797f0e03b0551a6bb), and [issue #5](https://github.com/ameenalqershi/ArchPulse/issues/5).

The 192-row public `requirements.csv` is a historical snapshot of Notion, and `requirement-baseline-lock.json` pins the **stable IDs**, the 21 families, and the S27–S33 specialist-specification crosswalk. The existing [native .NET traceability validator](../../tooling/ArchPulse.TraceabilityValidator/TraceabilityChecker.cs) checks graph references and dependency cycles; [native baseline validator](../../tooling/ArchPulse.TraceabilityValidator/RequirementBaselineChecker.cs) additionally blocks silent ID replacement, malformed task lists, missing acceptance descriptions, unreviewed family changes, and S27–S33 task/specification mismatch.

```sh
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build -- --root . --baseline
dotnet test tooling/ArchPulse.Tooling.Tests/ArchPulse.Tooling.Tests.csproj -c Release --no-build
```

## Changing the snapshot safely

1. Inspect the current Notion S26 source, relevant Sxx details, and Txxx task relations. Record the Notion revision time and reviewable change rationale.
2. Update `requirements.csv` and its lock **together**, only when an authorized reviewer has reconciled added/retired/renamed IDs. ID changes may be breaking changes requiring ADR and consumer migration; never regenerate the lock merely to silence CI.
3. For each addition, record expected behavior, test/acceptance expectation, and real Notion Txxx links. Reflect the current spec family mapping, but do not infer a completed implementation from a planned requirement.
4. Update this guide when the snapshot contract changes, run both validators and negative tests, obtain reviewer approval, and link the PR evidence in the task ledger.
5. Never mark a requirement `Verified` in this snapshot: implementation receipts belong in a future separately reviewed evidence registry with source SHA, test IDs, environment, and approval status.

**Limitations:** This lock pins the public 2026-11-10 index; it does not prove live Notion synchronization, the full Charter's semantic completeness, authoritative acceptance of every proposed design, or a successful feature test. Entire families and requirements must still be reviewed against S00–S33 before T001 can be accepted.
