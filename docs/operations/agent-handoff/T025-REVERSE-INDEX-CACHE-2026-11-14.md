# T025 / S05 — per-revision reverse call-index reuse (2026-11-14)

- Base: GitHub main `2e755869c672caf384c200106534f1e1cecab658` (check latest before publication).
- Isolated authorized VPS checkout: `/tmp/archpulse-agent-impact-index-20261114`, branch `perf/t025-reuse-impact-reverse-index-20261114`.
- Authority: [Notion T025](https://app.notion.com/p/4143830ba34d813b9f70f736bdcecd07); specification S05 is proposed. T025 is In Progress, not Verified.
- Root cause: `FeatureImpactAnalyzer.Assess` called `BuildReverseCallGraph(entry.Graph)` for each of up to 500 events, despite all events using the same before/after snapshots.
- Change: lazy reverse index for each exact revision separately, and one-time feature/member sorting. Preserve existing event dedup, static evidence and bounds.
- Regression: mixed before/after event evidence (one path on each revision) and synthetic 240 change events showing deterministic, complete examined count and intentionally deduped 121 candidate signals.
- Before merge: locked restore, Release build, full xUnit, native validators, dotnet format, exact published SHA clean-clone reproduction, hosted CI and human architectural review.
- Nonclaims: no actual Git patch/Guardian integration, AST body changes, observed coverage, production performance benchmark or full T025 acceptance.
- Next agent: inspect GitHub main/PR status before any retry or changes. Preserve other agents' work and never expose unauthenticated loopback services.
