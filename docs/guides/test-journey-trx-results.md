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

Future separately reviewable slices: Roslyn-proven direct test-to-method mapping with evidence, exact isolated test execution under explicit trust and authorization, test-root Activity instrumentation, bounded OpenTelemetry span correlation, privacy-conscious replay and in-place badges on existing architecture nodes. No S31/S33 acceptance, complete trace, frontend integration or production readiness is claimed here.
