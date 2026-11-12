# .NET Native Foundation & Source Graph — Experimental

This is an **implemented and tested subset**, not the full ArchPulse
production platform. The rebuild branch is rebuild/dotnet-native-foundation
rooted at SHA 6602183884bfb7b6dcd24803fb1d5db1ccdc28db.

## Free, native .NET toolchain

The project uses the .NET 10 SDK selected by global.json. No Python or
paid AI provider is required for the active build, validators or Roslyn CLI.

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    dotnet test ArchPulse.sln -c Release --no-build
    dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build
    dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build

The validators are offline structural and registry checks. Their dated
baseline (34 proposed specifications, 205 tasks, 192 requirements) was
reconciled against the connected Notion source on 2026-11-12. It must be
updated with a traceable decision when Notion changes; it does not
demonstrate feature acceptance.

## Roslyn source graph

    dotnet run --project apps/cli/ArchPulse.Cli -c Release -- index engine/ArchPulse.Core --revision "$(git rev-parse HEAD)" --project ArchPulse.Core > graph.json

The CLI emits JSON nodes and edges with source locations, stable
declaration IDs and explicit Static evidence. Pass the source revision
matching the supplied code; the CLI cannot independently verify that
the working tree has no modifications. Read Diagnostics for missing
dependencies and unresolved calls. External calls are not fabricated
as internal graph edges.

**Supported in this initial slice:** C# namespace/class/interface/record/
enum/method/property nodes, resolvable in-project semantic method calls,
line/column evidence, overload-aware IDs, explicit revision and JSON CLI.

**Not yet supported:** MSBuild solution loading, external package references,
incremental graphs, durable graph versioning, architectural rules,
FeatureAtlas, DataJourney, Web Studio, MCP Apps, runtime traces, Futures,
AI collaboration or production hardening. S02/S03 remain incomplete.

## Legacy Python retirement

The four prior foundation/traceability Python scripts were analyzed and
rewritten in native .NET, including positive/negative dependency/CSV tests
and the immutable handoff schema/audit checks. The preceding GitHub Actions
run 43247535851 passed with 28 tests and the .NET implementation.
The removal commit must also pass both required CI jobs before merging.
Prior historical evidence receipts remain intact in git history and
the verified VPS recovery bundle.
