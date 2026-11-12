# Native .NET rebuild status — 2026-11-12

Work in progress; this does not establish S00–S33 product acceptance.

## Baseline and recovery
- Requirements are Notion S00–S33. All 34 specification pages read; Proposed status.
- Public repository: https://github.com/ameenalqershi/ArchPulse
- Main SHA: 6602183884bfb7b6dcd24803fb1d5db1ccdc28db
- Independently verified complete-history bundle and SHA256 at /opt/archpulse-backups/2026-11-12-dotnet-rebuild/
- Independent rebuild branch and worktree at /opt/archpulse-rebuild-20261112. Main and unrelated worktrees unchanged.

## Code and evidence
Native C# tools validate SPDX, Apache License, JSON, XML, documentation links, registry IDs and S/T/R traceability including dependency DAG. Roslyn semantic indexer reads trusted .NET projects with MSBuildWorkspace, derives revisioned symbol graph, method calls and source spans with unknowns explicitly recorded. Graph exposes stable IDs and Mermaid export. Guardian evaluates forbidden dependencies and expiring scoped human waivers. CLI indexes trusted projects. ASP.NET Core loopback-only server returns real static GraphSnapshot and exact node selection. TypeScript/React Flow studio has pan/zoom, selection, search, inspector and relationship navigation.

The clean baseline repository indexed to 24 nodes, 28 edges, 1 unresolved call, with a nonzero CLI exit code for diagnostics. Output located at /opt/archpulse-backups/2026-11-12-dotnet-rebuild/verified-baseline-graph.json. Old Python validation comparison passed 16 legacy tests. Native CI passed on initial PR head ae8272ba712308280f498ea044503d426587c7f3, run 43248347098; legacy scripts are retired in a follow-up PR commit and must be verified on its new head.

## Unimplemented
Incremental indexing, rename reconciliation, persistence, FeatureAtlas, DataJourney, runtime/OTEL instrumentation, test journey, Futures engine, MCP Apps, agent gateway and recovery, secure sandbox, comprehensive RBAC, deployment and S00–S33 full acceptance are not implemented.

## Verification and next safe step
Local C# Core/Tooling/Roslyn/Guardian/Server tests passed at different recorded runs; check fresh full suite and exact PR head CI. Web Studio production build, API HTTP smoke and Vite proxy smoke passed; no browser/MCP host end-to-end confirmation. Keep exact GitHub required CI job names foundation-build-test and foundation-document-check. Before retiring Python run and confirm native CI, then update old documentation references. Do not claim product completion.
