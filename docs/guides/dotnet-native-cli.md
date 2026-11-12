# .NET Native Foundation & Source Graph — Experimental

This is an **implemented and locally tested subset**, not a full ArchPulse product acceptance.
Canonical design sources: Notion S01/S02/S03/S17/S25/S26. Initial rebuild branch:
rebuild/dotnet-native-foundation, rooted at 6602183884bfb7b6dcd24803fb1d5db1ccdc28db.

## Prerequisites

Use the SDK selected by global.json (.NET 10). On an authorized copy:

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    dotnet test ArchPulse.sln -c Release --no-build
    dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build
    dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build

The two validators are **offline, deterministic foundation/index checks**. Their current
snapshot baseline matches the live 2026-11-12 Notion counts (34 specifications, 205 tasks,
192 requirements); rebaseline only after reconciling current Notion and updating tests.

## Actual Roslyn graph export

    dotnet run --project apps/cli/ArchPulse.Cli -c Release -- index engine/ArchPulse.Core --revision "$(git rev-parse HEAD)" --project ArchPulse.Core > graph.json

The CLI emits JSON nodes and edges with source locations, stable declaration IDs and
explicit Static evidence, all at the **caller-supplied revision**. Only pass the actual
matching source revision; the CLI cannot independently verify that the working tree
matches this SHA. Inspect Diagnostics for unresolved symbols or syntax problems.
External framework calls are not fabricated as internal edges.

**Implemented in this branch:** source-scoped class, namespace, interface, record,
enum, method and property nodes; semantic-model method-to-method call edges in the
supplied source assembly; line/column evidence; stable overload-aware IDs; version
labels; CLI JSON export; positive and negative tests.

**Not implemented yet:** MSBuild solution loading, external NuGet symbol binding,
incremental indexing, Graph persistence, Git diff correlations, full interface/override
edges, all method forms, Guardian, Web Studio, MCP Apps, telemetry or Futures.
No trace is Observed merely because an AST call exists. Build success is not
equivalent to acceptance of S02 or S03.

## Python migration boundary

The four historical scripts/*.py files remain **temporarily** as migration
references while the rewritten .NET CI workflow awaits an actual successful
GitHub-hosted run on this PR. They are not invoked in the new workflow.
Their removal requires review of equivalence and CI receipts; do not remove
solely because local test execution passed.
