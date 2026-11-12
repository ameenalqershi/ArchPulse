# Test Execution Journey: native results-only TRX evidence (S31/S33 partial)

**Scope:** import an existing trusted local TRX test-report artifact into an evidence-scoped C# data model. Source specification: [S31](https://app.notion.com/p/4143830ba34d81a48fbedb25bd391e1f) and [S33](https://app.notion.com/p/4143830ba34d81cbab32f904bc5784bb). The importer is read-only. It never runs tests, loads an untrusted assembly, connects to production or instruments code.

## Reproduce offline

Run from an authorized checkout with .NET 10 SDK. These example test commands are initiated by the engineer, never by the importer:

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    dotnet test tests/ArchPulse.Core.Tests/ArchPulse.Core.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=archpulse-tests.trx'
    dotnet run --project tooling/ArchPulse.TestJourney -c Release --no-build -- --trx tests/ArchPulse.Core.Tests/TestResults/archpulse-tests.trx

Optional arguments --tested-sha and --graph-sha accept lowercase 40-character Git commit SHAs. This importer **does not independently verify that the TRX file was produced by that Git revision**. The freshness result is SameDeclaredRevision, Stale or Unknown, based only on optional caller-declared metadata. A same-revision label is not an authenticated record.

## Evidence boundaries

- Source inputs: bounded (maximum 16 MiB) local VSTest TRX, maximum 20,000 UnitTestResult entries. The CLI rejects external XML DTDs/entities and non-TRX roots and keeps SHA-256 of exact bytes.
- Structured output: test identifier, runner-reported outcome, attempt ID, numeric duration when valid, declared test class/method only when metadata is safe, outcome counts, and content digest. Repeated/parameterized attempts remain distinct. Raw testName parameter values, assertions, stdout, stderr, stack traces and attachments are never exported.
- Runner outcome Passed does **not** establish that a particular application method was called. Test-class/test-method metadata refers only to the test method and does not establish association to a target SUT symbol.
- TraceStatus is always UnobservableFromTrx, and MethodCallsObserved, ValuesCaptured and PerTestCoverageProven remain false. TRX does not give call order, returns, values or per-test coverage. Static call paths must never be marketed as observed traces.
- Unknown/missing/damaged durations are represented by null, not by zero. Unrecognized runner outcome becomes Unknown. No process launch or remotely triggered test run exists in this module.

## Verified first slice and exclusions

This slice is exercised by native xUnit tests, and scripts/ci-test-journey-results.sh reads a real TRX produced by the preceding repository test step. The script is not a sandbox runner and does not execute source code in response to an MCP request.

Completed separate partial slices now include Roslyn-proven direct test-to-method
static source links (PR #39), in-place Web Studio/MCP Apps Method Inspectors
(PR #41), and the offline **name-only runner case candidate matcher** described
below. Future separately reviewable slices include independently verifiable
TRX-to-test-binary/revision provenance, real runner case identity mapping,
authorized isolated test execution, test-root Activity instrumentation,
bounded OpenTelemetry span correlation, privacy-conscious replay and
explicitly provenance-labelled results inside the UI. No S31/S33 acceptance,
complete trace, runner identity verification or product readiness is claimed.

## Offline TRX-to-source name candidate reconciliation (S33 partial)

A read-only native C# matcher in tooling/ArchPulse.TestJourney now compares
existing sanitized VSTest TRX **className + method name** to Roslyn source test
**declarations** in the exact saved graph revision. It never evaluates a
project, runs a test, rewrites the graph or invokes a remote service. This is
not a runner-discovered test case identity binding and is not a method-to-SUT
execution proof.

After a trusted local build, CLI source index, and separately run VSTest:

    REVISION="$(git rev-parse HEAD)"
    STORE="/tmp/archpulse-candidate-reconciliation"
    dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- test-links ArchPulse.sln --revision "$REVISION" --store "$STORE" > /tmp/archpulse-static-source.json
    dotnet run --project tooling/ArchPulse.TestJourney -c Release --no-build -- --trx tests/ArchPulse.Core.Tests/TestResults/archpulse-tests.trx --tested-sha "$REVISION" --graph-sha "$REVISION" --project ArchPulse --store "$STORE" > /tmp/archpulse-case-candidates.json

The candidate report uses schema
archpulse.test-journey.source-candidates/v1 and includes TRX SHA-256,
runner RunId, graph/source revision, separate attempt and case IDs, sanitized
test class and method names, runner outcome *for that attempt only*, exact
optional candidate Roslyn SymbolId/test project/source evidence, and explicit
gaps. Sensitive raw TRX logs/attachments/testName values are never copied.

Statuses are distinct:

- UniqueNameCandidateUnverified — exactly one source declaration has the
  same fully qualified class and method name, and both caller-declared SHA
  labels match the exact saved graph SHA. **This is a candidate only.**
- AmbiguousExactName — more than one source test declaration with that name
  (including different projects or overloaded method signatures); no source
  SymbolId is assigned.
- NoExactSourceCandidate — no exact match; substring, simple method names,
  case-folding and generic-symbol guesses are not allowed.
- MissingOrUnsupportedRunnerName — no safe metadata class/method, or name
  grammar unsupported.
- RevisionNotCorrelatable — SHA metadata missing, stale, or cannot be
  compared. No source candidate is assigned for any attempt.

RevisionAssessment is MatchingDeclaredRevisionUnverified,
MismatchedDeclaredRevision, MissingDeclaredRevision, or
UnsupportedRevisionIdentity. **Matching labels are caller provided and
do not prove that the TRX was produced by that source checkout or test
binary.** The exact runner TestCaseId is not a Roslyn SymbolId. Parameterized
cases and retries remain distinct; the matcher does not aggregate/choose the
last outcome or attach pass/fail to the production method.

SUT outcome always has NoTestToSutRuntimeProof, each attempt's
SutExecutionEvidence is Unobserved and PerTestCoverageProven is false.
No separate test-run data is mixed with the S31 static link artifact,
and no API exposes this correlation report yet. A recorded OpenTelemetry
test-root trace with validated runner identity is required for real
Test Execution Journey call/return tracing.

The hosted CI job runs
scripts/ci-trx-source-candidates.sh after the actual repository VSTest run
and uses the same locked native .NET CLI to assert nonzero exact-name
candidates, same *declared* SHA provenance, and no forged observed coverage.


## Explicit trusted local test invocation and content digest receipt (S33 partial)

The separate **local-only** CLI capture command is opt-in. It executes an
already-built native test project with the caller's operating-system
privileges. **This is not a sandbox. Do not use it with untrusted checkouts,
external repositories, production environments, a publicly exposed API,
or arbitrary remote MCP callers.** No API/MCP tool dispatches it.

In a *clean* trusted checkout with locked packages restored and Release
output already built:

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    RESULT_DIR="$(mktemp -d)"
    dotnet run --project tooling/ArchPulse.TestJourney -c Release --no-build -- \
      --capture tests/ArchPulse.Core.Tests/ArchPulse.Core.Tests.csproj \
      --root . --output-dir "$RESULT_DIR" --allow-local-execution > /tmp/archpulse-local-run-receipt.json

The output directory must already exist, be empty, and be outside the
repository. The CLI does not accept additional runner arguments or arbitrary
non-test projects. The subprocess uses `dotnet test` with
`--no-build --no-restore -c Release`, a private output directory, and
a three-minute timeout. It discards stdout/stderr rather than publishing
possibly private test inputs, exception details, access tokens or secrets.
This does not make executed code safe: only trusted local test projects
should be explicitly authorized.

Evidence written into the JSON receipt:

- Local `git rev-parse HEAD` and `git status --porcelain` before and after
  execution; both must report the same SHA and a clean checkout.
- SHA-256 of the **conventional Release test DLL** before and after test
  invocation; mutation is rejected. This proves the CLI read identical
  local bytes, NOT that the runner loaded that exact DLL or that those bytes
  were built from the declared Git revision.
- SHA-256 and parsed run ID of the fresh private VSTest TRX artifact and
  the observed `dotnet test` process exit code. A failed test run, if
  a valid TRX exists, still yields an attempt-scoped evidence receipt with
  nonzero CLI exit code; it is not misreported as a successful process.
- Sanitized attempt counts and runner outcome totals, with **no** raw logs,
  raw parameter values, stack traces, arguments or runtime method traces.
- `SutMethodCallsObserved=false`, `PerTestCoverageProven=false`,
  `SutExecutionEvidence=NoSutCallOrReturnEvidence`. Runner metadata,
  a passed test, git checks and local DLL digests cannot prove the
  identity of a runtime test case to a Roslyn method or SUT call coverage.

The JSON schema label is
`archpulse.test-journey.local-run-receipt/v1`. The prior results-only
and static source-name candidate import commands remain unchanged.
The CI smoke at `scripts/ci-local-test-run-capture.sh` validates the opt-in
flag, the clean source check, and actual TRX content hashing with the
trusted Core.Tests project. CI invocation is a deliberate, already trusted
runner operation, never a request through the unauthenticated loopback
Graph Server or MCP Apps host.

**Unfinished S33 features:** independent test binary provenance attestation,
runner `TestCaseId` identity with source declarations, an isolated execution
sandbox, safe Activity/trace context propagation, observed SUT spans/calls,
bounded privacy-reviewed replay and native interactive results in Web
Studio/MCP Apps. No acceptance or production readiness is asserted.
