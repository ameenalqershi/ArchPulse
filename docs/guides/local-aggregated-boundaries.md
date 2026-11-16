# Local aggregated boundary edges (T122 / S27 — experimental)

This read-only prototype groups **existing** graph interaction edges crossing an exact canonical scope. It neither manufactures an architectural relation nor changes the original `GraphSnapshot`. The local planner belongs to `ArchPulse.Guardian` and depends only on the existing Graph and Guardian contracts.

## Contracts

- `AggregatedBoundaryPlanner.Plan(graph, scopeId, findings?, maxScanEdges)` returns exact project/revision/scope, explicit `AuthorizationNotEvaluated`, total underlying crossing edges and grouped counts.
- Group key = direction (Inbound/Outbound), exact relation, nearest **external canonical project** (or canonical root if a project is absent). Internal edges, external-to-external edges and parent containment do not become crossing edges.
- Each group separately counts `Designed / Static / Observed / Inferred / Verified / Unknown` evidence. `Verified` means only the existing source's declared EvidenceKind; it is **not** independent runtime verification.
- Optional existing `ArchitectureFinding` values are exact-edge checked, counted by distinct EdgeId, including **Blocker** violations even if omitted from the first 20 preview IDs. `Waived` and `Unknown` are separate; no empty collection establishes zero risk. Non-edge-specific policy findings (including unevaluated blockers) remain visible in separate summary counts, not silently assigned to a bundle. Caller-supplied policy findings are labeled `CallerSuppliedFindingsUnverified`.
- `WitnessesPage(graph, scopeId, groupKey, pageSize, afterEdgeId)` re-exposes actual source-backed `GraphEdge` objects in stable `EdgeId` order (up to 100/page). Unknown or cross-group cursors fail explicitly. The local raw-ID cursor is **not authenticated**.
- A bound of 100,000 scanned edges and 500 groups is enforced by explicit failure, not silent omission. Snapshot validation is still whole-graph; no claim of end-to-end constant memory or a bounded snapshot read.

## Trust and rollout limitations

No tenant permission evaluation, RBAC, authentication, trusted policy approval, rule-set completeness, runtime sampling/coverage proof, remote API exposure, Studio integration, persistent index, signed cursor, or large-corpus benchmark. This is not a security enforcement gate and is never safe to expose via unauthenticated public API. Source-file paths may be sensitive and must only be returned to an authorized local caller; the current prototype does not implement this authorization.

T120/T121/T019 and S27 are still partially implemented/proposed. Do **not** mark T122 Verified or ADR-0022 Approved from a green local/hosted test alone.

## Reproduction

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build
dotnet run --project tooling/ArchPulse.RepositoryValidator -- --adr
dotnet run --project tooling/ArchPulse.TraceabilityValidator -- --baseline
```

Focused tests: `tests/ArchPulse.Guardian.Tests/AggregatedBoundaryPlannerTests.cs`. They cover 250 calls with a late blocker, 100-item witness pages, real local Guardian evaluation, unscoped blockers, evidence-mode/direction/relation grouping, unknown/waived findings, invalid cursors, corrupt graph, group-budget and scan-budget rejection. No performance measurements or access-control E2E claim.
