# T011/S02 — retained-identity structural graph diff (2026-11-14)

- Base: `main` `2e755869c672caf384c200106534f1e1cecab658`, snapshot before any merge of open PRs #54-#60.
- Branch: `fix/t011-detect-changed-graph-evidence-20261114`.
- Isolated authorized VPS: `/tmp/archpulse-agent-graph-diff-20261114`.
- Problem: two snapshots retaining identical GraphNode.SymbolId / GraphEdge.EdgeId could differ in node label, parent, source span or edge meaning but `GraphSnapshotStore.Compare` reported empty identity delta.
- Change: additive `ChangedNodes`, `ChangedEdges` fields on `GraphDifference`; structural and source evidence comparison ignores revision string only.
- Tests: retained-identity label/source movement, relationship edit, ownership move, revision-only unchanged, method-body-only explicit blind spot, HTTP response. Focused Roslyn GraphSnapshotStoreTests and LocalGraphApiTests verified locally.
- Out of scope: content hashes/AST body changes, incremental event-sourcing T010, Git patch analysis, FeatureImpactAnalyzer semantics, runtime/observed evidence, permissions.
- Run locked restore, Release build, entire xUnit solution, native RepositoryValidator and Traceability baseline, dotnet format; then publish only verified blob hashes, re-clone exact remote SHA and inspect hosted CI. Record GitHub link and handoff in Notion T011 (View Mode for table reads).
- Maintainer review before merge. Do not mark T011, S02, or related architecture Approved or Verified.
