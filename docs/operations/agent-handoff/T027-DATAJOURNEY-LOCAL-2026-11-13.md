# ArchPulse agent handoff — T027/S06 local DataJourney (2026-11-13)

## Context and parent
- [Notion T027](https://app.notion.com/p/4143830ba34d8196a273f6dccadea86a) /
  [S06](https://app.notion.com/p/4143830ba34d8141940af68ed36538be)
  **still Planned/Proposed**, not formally Verified.
- Expected parent is GitHub **main** exact SHA
  `78e3e2eea4fa8864d266cb4b005b3fc133a37815`,
  which already contains PRs #46–#49 merged, 233 passing .NET xUnit
  and 10 MCP tools. No outstanding PRs when started.
- Work uses isolated Linux checkout
  `/root/archpulse-t027-20261113` branch
  `feat/s06-datajourney-local-t027-20261113` — do not interfere with
  active remote desktop/other agent worktrees.

## Scope delivered
- `engine/ArchPulse.Graph/LocalDataJourney.cs`: revision-bound immutable
  metadata-only report schema `archpulse.datajourney-local/v1`,
  `EvidenceKind.Static` and `RuntimeStatus.Unknown`, bounded
  findings/diagnostics and fail-closed corrupt sidecar.
- `engine/ArchPulse.Roslyn/LocalDataJourneyAnalyzer.cs`: Roslyn semantic
  analyzer for explicit fully-qualified annotations, local variable
  assignments and reassignment, explicit sink calls, declared source methods,
  sanitizer *claims*, and return propagation. Branches, unknown methods,
  unsupported syntax and unmarked inputs become `Unknown`.
- SolutionGraphAnalysis extended additively; trusted .NET CLI
  `datajourney-local`, `datajourney-show`; HTTP read-only
  `GET /api/graphs/{project}/{revision}/datajourney-local`; eleventh
  read-only MCP `archpulse_datajourney_local`. No MSBuild evaluation from
  remote clients.
- `examples/datajourney-local` real .NET10 fixture, including misleading
  `DeclaredSanitizer` implementation which returns input unchanged.
- `scripts/ci-datajourney-local.sh` includes positive, negative, return,
  safe-subset, unknown and same-name annotation decoy assertions, exact
  revision sidecar and tampering rejection, and explicit 0/2 false positive
  rate on two *controlled* negatives (not a production accuracy claim).
- MCP's actual .NET protocol smoke expanded to 11 tools and source-only
  flow report; unit tests exercise Roslyn, HTTP and MCP failure modes.
- [Reproduction and privacy limitations](../../guides/datajourney-local.md).

## Reproduce and verify

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/datajourney-local/DataJourney.sln --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release
dotnet test ArchPulse.sln -c Release --no-build
bash scripts/ci-datajourney-local.sh
bash scripts/ci-mcp-protocol.sh
bash scripts/ci-feature-atlas.sh
bash scripts/ci-feature-discovery.sh
bash scripts/ci-feature-impact.sh
bash scripts/ci-feature-review.sh
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root .
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root . --handoffs
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root . --adr
dotnet run --project tooling/ArchPulse.TraceabilityValidator --no-build -c Release -- --root . --baseline
```

Before merging, check GitHub workflow result at **exact remote HEAD SHA**
for Foundation CI, ADR and CodeQL if triggered, including failures,
cancelled jobs and dependencies. Preserve protected-main PR-only
requirements. If hosted CI fails, do not claim review-ready.

## Known limitations / safety
- Annotations are source declarations, not proof of data sensitivity or
  sanitizer correctness. `NoCandidateInSubset` means **only** no tracked
  candidate in an explicitly limited straight-line slice, never safe.
- No control-flow graph, alias analysis, nested calls, interprocedural or
  cross-service flow, EF/database/event sinks, async message payloads,
  reflection, dynamic SQL, side-effect tracking or observed runtime yet.
  This is a **partial T027**, not full T027/T028 or S06.
- No source literal/input/return values are captured, but source identifiers
  and paths themselves could be identifying; keep report in trusted local
  authorized stores, avoid publicly exposing loopback services.
- MSBuild evaluation of untrusted .sln/.csproj is unsafe; do not run on
  untrusted code without sandbox. Graph and DataJourney sidecars are
  immutable per declared revision but not cryptographically signed, and
  human review and actual security enforcement are still required.
- Needs expanded corpus and measured false positives on real code,
  validated sanitizer contracts, real branch/loop CFG and interprocedural
  propagation, S06 privacy policy and external reviewer gate. Preserve
  agent continuity and explicitly mark incomplete items in Notion.

## Integration policy
- Primary stack C#/.NET 10, Apache-2.0, no Python tooling or paid AI APIs.
- Code only with explicit permissions; no remote production deployment.
- Do not falsely claim "safe", "Observed" or "Verified" from static reports.
- Canonical graph is unchanged; this is only a metadata sidecar.
