# T024 source-signal discovery handoff — 2026-11-13

## Work identity

- Project: ameenalqershi/ArchPulse, Apache-2.0, C#/.NET 10.
- Stacked parent: PR #46, exact GitHub SHA
  `c0c65b893b141d48c119a0cfcc4d6c6996307014` (T023 partial).
- Work branch: `feat/s05-feature-discovery-t024-20261113`.
- Notion: S05 Proposed, T024 Planned, dependency T023. Implementation **partial**,
  not a change to formal acceptance status.
- Isolated authorized VPS checkout:
  `/root/archpulse-t024-20261113`; the prior T023 checkout and all
  previously present agent branches/worktrees were not modified.

## Implemented

- `engine/ArchPulse.Roslyn/FeatureCandidateIndexer.cs`: conservative
  method-level Roslyn semantic discovery with real graph method identities.
- `engine/ArchPulse.Graph/FeatureDiscoveryContracts.cs`: bounded validated
  inferred-business-signal receipts and immutable exact graph/revision sidecar.
- Extend `SolutionGraphAnalysis` additively; CLI `feature-discover` and
  `feature-signals`, read-only loopback ASP.NET API and eighth MCP tool.
- New real ASP.NET Core 10 MVC attribute golden solution for invoices, a named
  unregistered inventory handler, a compile-only test marker and decoys.
- Source signal type distinction and exact evidence source location retained;
  runtime Unknown and business `Inferred`, with no automatic catalog membership.
- CI includes locked fixture restore, actual CLI and MCP smokes and native tests.

## Proof requirements

Run on the exact final feature head; do not interpret local success as hosted CI:

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/feature-signals/FeatureSignals.sln --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release
dotnet test ArchPulse.sln --no-build -c Release
bash scripts/ci-feature-discovery.sh
bash scripts/ci-mcp-protocol.sh
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root .
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root . --handoffs
dotnet run --project tooling/ArchPulse.TraceabilityValidator --no-build -c Release -- --root . --baseline
```

Also review PR-head GitHub `foundation-build-test`,
`foundation-document-check`, `web-studio-build-test`, `adr-contract`,
CodeQL analysis and NuGet manual license-reviews **without suppressing any gate**.

## Remaining gaps and governance

- T024 missing minimal API route discovery, business intent grouping, real
  MediatR/DI registration proof, OpenAPI, false-positive adjudication and
  feedback. Partial implementation must not be marked Verified.
- T025/T026 Guardian/PR impact, reviewed/locked feature corrections are pending.
- S08 runtime tracing, S13 sandbox isolation and S16 public authenticated MCP
  are not implemented.
- The compile-only xUnit marker is **not** a real runner and does not prove
  anything about test execution. Controller attribute name is not deployment.
- Current HTTP and MCP servers stay **127.0.0.1 only** and unauthenticated.
  Do not tunnel or expose without reviewed identity/workspace permissions.
- Review PR #46 first; merge stacked T024 PR only after its parent and
  exact hosted SHA are reconciled. Keep separate from existing CodeQL
  admin issue #44 and never reintroduce Python.
