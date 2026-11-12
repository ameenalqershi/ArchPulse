# Retire all remaining active Python tooling — 2026-11-12

Status: native C# replacements implemented and tested; exact GitHub PR and merge
status must be independently checked from the published branch. This is not
claiming that all legacy optional CLI workflows have equivalent replacements.

## Scope and retained evidence

The final active 12 Python source files under scripts/ were removed:

- audit_nuget_licenses.py and test_nuget_license_audit.py
- requirement_registry.py and test_requirement_registry.py
- validate_requirement_baseline.py and test_requirement_baseline.py
- validate_adr.py and test_adr_validator.py
- validate_handoff.py, validate_handoffs.py, test_handoff_validator.py,
  and test_handoff_validation.py

Prior to removal, the tools were verified on the authorized Linux VPS: all
six active legacy validator CLIs succeeded, and Python unittest discovery
returned 87 passing tests. Source files and historical test receipts remain
in Git history, not in the current revision. No Git history was rewritten.

## Native authorities and regression guards

All production/core tooling and CI remain C#/.NET 10, plus TypeScript/React
for the web client:

| Previously active Python checker | Native replacement |
| --- | --- |
| ADR JSON/schema/lifecycle/receipt/narrative | RepositoryValidator --adr, AdrChecker.cs, AdrValidationTests.cs |
| S25 checkpoint schema/idempotency/secrets | RepositoryValidator --handoffs, HandoffAllChecker.cs, NativeToolRetirementTests.cs |
| S26 proposed requirement ID lock, family/trace links | TraceabilityValidator --baseline, RequirementBaselineChecker.cs, NativeToolRetirementTests.cs |
| Proposed specification/requirements registry integrity | TraceabilityValidator, TraceabilityChecker.cs, native negative tests |
| NuGet metadata license audit | LicenseAudit and LicenseAudit.Tests, native offline package checks |

The original native RepositoryValidator baseline also retains its existing
FOUNDATION-AUDIT schema test, while --handoffs validates *all* committed
v1 checkpoints and preserves a digest-bound waiver for one historical file
whose recorded content predates idempotency vocabulary. No new file benefits
from that waiver.

To verify with locked dependencies on a trusted checkout:

    dotnet restore ArchPulse.sln --locked-mode
    dotnet build ArchPulse.sln -c Release --no-restore
    dotnet test ArchPulse.sln -c Release --no-build
    dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root . --adr
    dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root . --handoffs
    dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build -- --root . --baseline
    dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build -- --root .
    dotnet run --project tooling/ArchPulse.LicenseAudit -c Release --no-build -- --repo .

GitHub ADR Governance Contract keeps the existing adr-contract job name, but
now uses only native C# and xUnit adversarial tests. Foundation CI checks the
S25/S26 native gates and rejects tracked Python source files, without weakening
the protected main/PR checks. No paid AI package or Python interpreter is
required for the active engineering pipeline.

## Explicit migration gaps and limitations

The legacy optional requirement_registry.py argument-driven manifest export,
single requirement ID lookup, and deterministic JSON export command were not
ported as a feature-for-feature native user-facing CLI. The canonical CSV
registry, requirement lock and native traceability audits are preserved.
Do not represent the retired export feature as implemented without a separate
specification and tests.

Both ADR and agent handoff validation are structural. Neither authenticates
a GitHub reviewer, reconstructs the actual approval lifecycle from Git history,
proves agent handoff, or guarantees the absence of all credential formats.
NuGet license reviews still require separate manual assessment where flagged.
Legacy audit references in dated checkpoint JSON documents are historical
receipts, not runnable commands for the current tree.

This cleanup is independent of completing S31/S33 or authorizing external
MCP transport. Static graph links and TRX name candidates do not establish
runtime traces, tested-binary provenance or SUT coverage.
