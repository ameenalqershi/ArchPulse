# Source-backed method ↔ test declarations (S31/S33 first static slice)

**Status:** experimental native C# implementation, partial [Notion S31](https://app.notion.com/p/4143830ba34d81a48fbedb25bd391e1f) and [S33](https://app.notion.com/p/4143830ba34d81cbab32f904bc5784bb). This is a **static direct-invocation projection**. It is **not** a complete runner-discovered test registry, test execution, per-case coverage, a trace, or approval of any S31/S33 requirement.

## Why this projection is deliberately separate from the architecture canvas

The canonical `GraphSnapshot` continues to represent actual C# declarations and Roslyn-resolved `Calls` edges. A `StaticTestAssociationReport` is a separate evidence document, to be queried by an existing **method SymbolId**, rather than introducing duplicate standalone TestCase boxes in the main diagram. Web Studio and experimental MCP Apps now query the saved report inside the Method Inspector.
No independent TestCase nodes are added to the master architecture canvas.
Full S31 badge/action and runner-discovery coverage is **not** implemented.

The projection identifies a C# method only when it has a semantic attribute type whose full name is one of:

- xUnit: `Xunit.FactAttribute`, `Xunit.TheoryAttribute`
- NUnit: `NUnit.Framework.TestAttribute`, `NUnit.Framework.TestCaseAttribute`
- MSTest: `Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute`, `DataTestMethodAttribute`

An attribute's type name is **not an authenticated test-runner discovery receipt**, which is why the report marks `RunnerDiscovery = NotRunnerDiscovered`. Custom wrappers, dynamically generated tests, parameter data, and runner-specific filters are not enumerated.

A relation is emitted only when the annotated test method has a **Roslyn-resolved direct invocation** whose existing source `Calls` edge leads to a **declared method in another project of the same indexed solution**. Calls to test-project helpers are not presumed to target the system under test. Invocations inside lambda expressions, anonymous functions, or local functions are excluded because their execution is not established by the containing test method's body. Overloads are distinguished by their exact Roslyn documentation SymbolIds, not method names.

Repeated direct calls from one test method to the same target are one association with multiple `CallSites` (source file, line, column and graph revision). Relations are labeled `DirectInvocation`, `StaticSourceOnly`, `Unobserved`, and `LastResult = Unknown`; no runtime test results, actual coverage or assertion status is inferred.

## Reproduce on an explicitly trusted local solution

> **Security:** MSBuildWorkspace evaluates the supplied .sln/.csproj files. Use only an authorized checkout. Do **not** run this on untrusted repositories or expose it as an unauthenticated MCP execution endpoint.

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- test-links ArchPulse.sln --revision YOUR_EXACT_SOURCE_REVISION --store /tmp/archpulse-graph > /tmp/archpulse-static-test-links.json

To select an individual method, use its **exact** `GraphNode.SymbolId` from the same solution graph:

    dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- test-links ArchPulse.sln --revision YOUR_EXACT_SOURCE_REVISION --target 'EXACT-SYMBOL-ID'

When no direct mapping is known, the target query reports `mappingStatus: Unmapped` and an empty `links` list. **Unmapped is not 0% coverage**, and a passing result from another run cannot be propagated onto that method. An unrecognized target SymbolId is rejected rather than guessed.

The API is `SolutionGraphIndexer.AnalyzeSolutionAsync(...)`, which returns the existing `GraphSnapshot` plus `TestAssociations`. `IndexSolutionAsync(...)` maintains its original graph-only contract. All evidence uses the graph's supplied source revision; **it is not cryptographically verified against Git**. Never interpret matching caller-provided revision text as verified commit provenance.

## Read-only Inspector lookup (Web Studio and MCP Apps)

The trusted local CLI command with --store <folder> persists both
the immutable graph and a distinct *.test-links.json document for the same
project and exact revision. Existing revisions cannot be overwritten with
different static evidence. The sidecar validator rejects missing source edges,
orphan test/target symbols, wrong revisions, fabricated observed outcomes,
duplicate links or call sites, and oversized documents.

With that trusted store, the loopback-only ASP.NET Core server at
127.0.0.1:5217 exposes:

    GET /api/graphs/{project}/{revision}/test-links?symbolId=URL_ENCODED_EXACT_SYMBOL

For an existing Method, a missing report yields NotIndexed, not a
claim of zero tests. A report without links for that method yields
Unmapped; evidence-backed direct links yield MappedDirectStatic.
Unknown source revision or symbol returns 404; invalid saved evidence
returns 422. Both UIs label any result Unknown, Unobserved and
StaticSourceOnly; no pass/fail, coverage percentage, trace or scoped
test execution is inferred. Web Studio cancels outdated selection fetches.

The sixth native MCP read-only tool, archpulse_linked_tests,
retrieves the same report when a Method is selected in the MCP Apps
HTML Inspector. The server must remain 127.0.0.1:5231 only;
it is unauthenticated and no real ChatGPT host acceptance test has
been performed. MCP host-simulator tests are only contract tests.

Use bash scripts/ci-static-test-links.sh after the locked .NET build
to prove generation of a real source-based graph and sidecar without
paid services or remote code execution. See the Web Studio README for
optional browser tests.

## Distinct TRX evidence is not joined yet

The separate [TRX results-only importer](test-journey-trx-results.md) parses actual VSTest runner outcomes without source call traces. Neither that report nor this static projection automatically proves which target implementation method ran. Future work must independently join an exact test-runner case/attempt ID to a declared test method, verify build/source revision, handle parameterized case identity, and produce scoped observed instrumentation before showing execution status on the original SUT node.

Required next steps: runner discovery adapters (xUnit, NUnit, MSTest), safe per-case joining, exact existing-method Inspector badges, authorized isolated test execution, per-test correlated `Activity` spans, replay with trace gaps, S16 permission enforcement and privacy guards. No TestCase node injection into the primary canvas.
