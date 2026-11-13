# T025 / S05 — structural candidate feature impact (2026-11-13)

## Work identity and dependencies

- Base: GitHub PR #47 (T024 partial), exact commit
  `d55822fe7a835b9b7584b10309e879f8eb76320b`.
- Parent of #47: PR #46 (T023), exact commit
  `c0c65b893b141d48c119a0cfcc4d6c6996307014`.
- Branch: `feat/s05-feature-impact-t025-20261113`.
- Isolated VPS checkout: `/root/archpulse-t025-20261113`.
  No pre-existing agent checkout was modified.
- Authority: [Notion T025](https://app.notion.com/p/4143830ba34d813b9f70f736bdcecd07)
  requires PR paths, tests and Guardian integration. **Only a structural
  graph-based partial implementation** is supplied. Do not mark T025 Verified
  or S05 Approved without human review and separate acceptance evidence.

## Implementation

- `engine/ArchPulse.Graph/FeatureImpactAnalyzer.cs`: exact two-revision
  graph structural delta plus human candidate FROM catalog. Source-backed
  added/removed node/change-site call-edge events, bounded reverse static
  `Calls` witness path, deterministic sorting and evidence labels.
- CLI `feature-impact`, read-only local HTTP
  `GET /api/graphs/{project}/feature-impact?from=...&to=...`,
  ninth read-only local MCP tool `archpulse_feature_impact`.
- No automatic business membership, no GitHub webhook, no direct git diff,
  no observed execution, test runner, Guardian rule admission or approval.
- `scripts/ci-feature-impact.sh`: ephemeral trusted invoices/inventory
  fixture, a real source edit, Roslyn reindex, candidate FROM catalog,
  positive structural impacts and negative missing catalog.
- `scripts/ci-mcp-protocol.sh`: official C# MCP protocol,
  ninth tool check, empty structural comparison not overclaimed and
  unknown revision rejected.
- xUnit tests for witness directionality, unreachable features, shared
  membership, inferred edges/nodes excluded, exact revision controls,
  deterministic report, negative HTTP/MCP no writes/forged catalogs.

## Verification checklist

Run and inspect the output for this exact commit, never infer CI from a
prior PR or from these instructions:

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/feature-signals/FeatureSignals.sln --locked-mode
dotnet restore examples/billing-multi-project/Billing.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build
bash scripts/ci-feature-atlas.sh
bash scripts/ci-feature-discovery.sh
bash scripts/ci-feature-impact.sh
bash scripts/ci-mcp-protocol.sh
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root .
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root . --handoffs
dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build -- --root . --baseline
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root . --adr
```

Also check production Web Studio build and Vitest, npm audit, offline NuGet
metadata audit (1 known pending manual license review), and hosted Foundation CI,
ADR Governance, and CodeQL for any changed PR head after publication. Keep
the prior standalone CodeQL admin issue #44 separate.

## Important limitations

- Change detector compares Graph IDs/edges only; edits to method bodies with
  unchanged call relationships can yield no events. Absence of impacts is NOT
  proof that a PR is safe or that a feature is unchanged.
- A static call witness is a potential impact explanation, not an observed
  runtime execution or test assertion. Candidate membership is human-designed
  or inferred, never independently verified by this component.
- Source revisions are supplied by caller; there is no signed clean HEAD or
  binary attestation, no cross-repo propagation and no data sensitivity review.
- Current MCP and HTTP services are local-only, unauthenticated, read-only.
  Do not forward or tunnel them to a public client before auth/RBAC/TLS/audit
  and service isolation.
- For full T025: integrate trustworthy Git body/AST diffs, actual PR head SHA,
  Guardian findings and acceptance contracts with source-to-test mapping.
  T026 human corrections/approval also remains outstanding.
- Review #46, then #47, then this stacked PR in order; do not merge branches
  out of order or rewrite unrelated agent work.
