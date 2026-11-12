# S25 development-agent handoff validation coverage (T117)

**Scope:** historical, public, source-controlled development-agent
checkpoints. They are not authenticated real-time cross-agent continuity
(S11), an independently verified takeover (T118) or a full secret scan.

## Active native C# checkers

| Native component | Scope | CI usage |
| --- | --- | --- |
| [HandoffChecker.cs](../../../tooling/ArchPulse.RepositoryValidator/HandoffChecker.cs) | Fixed immutable foundation audit envelope, declared IDs and status | Built into Foundation RepositoryValidator |
| [HandoffAllChecker.cs](../../../tooling/ArchPulse.RepositoryValidator/HandoffAllChecker.cs) | All committed S25 checkpoint JSON: v1 types, task/spec identity, SHA/branch, safe relative paths, effect idempotency, receipts, reconciliation and common secret signatures | Foundation CI checks --handoffs |
| [NativeToolRetirementTests.cs](../../../tooling/ArchPulse.Tooling.Tests/NativeToolRetirementTests.cs) | Missing/forged fields, sensitive keys, duplicate effects, unknown states, unsafe paths, schema failures | Native xUnit tests in Foundation CI |

The old PR #23 and PR #25 Python validator variants are preserved only
in Git history, and no longer exist as active files. The tested C# validator
includes an explicitly digest-bound waiver for a historical checkpoint whose
record predates idempotency keys; this waiver is unavailable to any modified
or new checkpoint. Passing native checks does not authenticate reported
GitHub/Notion effects, test reports, or another agent's actual takeover.

## Reproducible local verification

Run from a trusted checkout; Python and paid AI services are not required.

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root . --handoffs
    dotnet test tooling/ArchPulse.Tooling.Tests/ArchPulse.Tooling.Tests.csproj -c Release --no-build

Failures must be addressed by a reviewable commit, not changing historical
external-effect receipts to look successful. An independent receiving agent
must inspect current GitHub source SHA, CI, PR state, Notion task and known
blockers before describing an actual handoff as verified. See the
[independent receiver rehearsal](REHEARSAL.md), [PR #23 historical sample](T117-2026-11-10.json) and [PR #25 historical sample](T117-PR25-2026-11-10.json).

**Current status:** S25/T117 is partially evidenced. T118 and S11 runtime
continuity remain unverified; this offline validator cannot impersonate
human oversight or authorize remote operations.
