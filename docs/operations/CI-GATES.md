# Foundation CI and future quality gates

## Native .NET migration

The live Foundation CI branch rebuild/dotnet-native-foundation contains two jobs:

- foundation-build-test: locked restore, Release build, all xUnit tests,
  repository/handoff contract validator, and traceability validator.
- foundation-document-check: independent locked restore and native .NET
  repository, handoff and traceability validators.

The names of **both** jobs are retained exactly because the active GitHub
ruleset 26926696 for main requires them. In the past, document checks used
Python. That dependency has been removed from the active workflow.

The migration's first PR head 1e01c2600e60a8e3bc036a212116996ee301c9e1
passed GitHub Actions run 43246261098 (21 tests). The handoff-parity
commit 60c2da6394a975455d838d3ff70e9552db6485b7 passed GitHub Actions
run 43247535851 (28 tests). Both runs preceded this final Python-removal
patch: **do not consider this cleanup verified until a new hosted run
passes at its exact SHA**.

GitHub job tokens only have contents: read, and checkout credentials are
not persisted. This CI passing does not certify product completion.

## Local reproduction

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    dotnet test ArchPulse.sln -c Release --no-build
    dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build
    dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build

## Proposed later gates (not active yet)

Static analyzers, formatting, architecture policy conformance, dependency
license audit and SBOM; CodeQL/secrets/vulnerability scans; isolated
integration tests, benchmarks, accessibility; release signing and
artifact provenance. Never present them as implemented/green without
actual workflow evidence or configure them as required checks before
their first successful GitHub run.

References: S16, S17, S19, S20 and the
[rebuild report](REBUILD-2026-11-12.md).
