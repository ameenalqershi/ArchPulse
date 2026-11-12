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

## Experimental manually curated FeatureAtlas (2026-11-13)

T023/S05 now has an **implemented partial** native C# feature catalog that requires an already indexed exact graph revision and an operator-provided candidate mapping. See the [local FeatureAtlas guide](feature-atlas-local.md) for the `feature-import`, `feature-show`, read-only API and seventh MCP tool. This does not automatically discover business features, validate them by tests, observe execution or complete the proposed S05 architecture.

## Partial automatic source-signal discovery (T024/S05; 2026-11-13)

The new `feature-discover <trusted.sln> --revision <sha> [--store <path>]` CLI analyzes Roslyn-declared MVC method attributes, naming-only Handlers and framework-name-matched test declarations without calling AI providers. `feature-signals <project> --revision <sha> --store <path>` reads the exact immutable sidecar. These are **inferred business candidates**, not auto-approved FeatureAtlas definitions or observed runtime events. [Reproducible fixture and limits](feature-discovery-local.md).

## Legacy Python retirement

The four prior foundation/traceability Python scripts were analyzed and
rewritten in native .NET, including positive/negative dependency/CSV tests
and the immutable handoff schema/audit checks. The preceding GitHub Actions
run 43247535851 passed with 28 tests and the .NET implementation.
The removal commit must also pass both required CI jobs before merging.
Prior historical evidence receipts remain intact in git history and
the verified VPS recovery bundle.

## Milestone 2 — real multi-project indexing and graph persistence

This is a locally tested partial S02/S03/S04 implementation, not
complete product acceptance. MSBuildWorkspace loads a restored trusted
.sln/.slnx, resolves project references and semantic cross-project
method calls. An immutable JSON store persists exact graph snapshots
by project+revision, rejects provenance anomalies, supports idempotent
writes, evidenced shortest paths, and revision diffs. This is not yet
SQLite, incremental indexing, or distributed transaction support.

ArchitectureGuardian evaluates forbidden project-to-project relation
rules against sourced edges and produces a witness with revision and
source location. Unresolved/inferred data is not treated as a proven
violation. Its waiver Approver field is NOT authenticated and is
not a production approval authority.

On the trusted golden two-project example:

~~~bash
dotnet restore examples/billing-multi-project/Billing.sln --locked-mode
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- index-solution examples/billing-multi-project/Billing.sln --revision golden-fixture --store /tmp/archpulse-graph
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- graph-show Billing --revision golden-fixture --store /tmp/archpulse-graph
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- guardian Billing --revision golden-fixture --store /tmp/archpulse-graph --rules examples/billing-multi-project/forbidden-policy.json
~~~

The supplied policy deliberately prohibits a real Billing.Service to
Billing.Contracts call; it is an instructional fixture, not a policy
proposed for ArchPulse. Warning-level violations are reported but
do not fail CLI; Error/Blocker violations return a nonzero exit code.

SECURITY: MSBuild evaluates project files; this experimental component
must only be used on trusted projects and should move into an isolated
worker. NuGet restore initially blocked affected MSBuild and cryptography
XML packages. Dependencies were upgraded/pinned to Microsoft.Build
17.14.28 and System.Security.Cryptography.Xml 10.0.13 without
suppressing warnings. MSBuild.Locator runtime asset exclusions ensure
the SDK supplies the MSBuild assemblies. No full license audit yet.


## Complete Python tooling retirement (2026-11-12)

After migrating ADR governance, S25 full checkpoint validation and S26
requirement snapshot integrity to native .NET tooling, all remaining tracked
Python tool scripts and Python unittest files were retired from scripts/.
The native NuGet license auditor and existing .NET repository/traceability
validators continue to be required. Historical Python-backed evidence stays
in Git history; we do not rewrite dated checkpoint records. The optional
Python requirement manifest query/export CLI has **not** been ported exactly;
there is no current support claim for that historical utility.
