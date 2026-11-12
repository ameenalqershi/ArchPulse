# ArchPulse — instructions for human and AI development agents

**Read first:** [README](README.md) → [S00](https://app.notion.com/p/4143830ba34d81e9b337d04a89706acc) → [S01](https://app.notion.com/p/4143830ba34d814fa155d2870b8e6c26) → [S25](https://app.notion.com/p/4143830ba34d81f3a119d6a5f7c01ae4) → selected Txxx and its Sxx → relevant S11/S12/S16/S17/S20. Do not rely on a previous conversation memory.

## Canonical truths

1. Notion is canonical for **planned scope, specifications and initial tasks**. Code and repo history are canonical for **implemented changes**; CI and signed artifacts for observed verification. A proposed spec is not a shipped feature.
2. Keep `Designed`, `Static`, `Observed`, `Inferred`, `Verified`, and `Unknown` separate. Never convert Unknown to Verified without independent evidence.
3. The .NET engine is intended to be local-first and independent of editor and AI provider; adapters must not duplicate source analyzers. Architectural modules have explicit boundaries; changes need a reviewable ADR when cross-module.
4. Mermaid source is mandatory for **every** candidate architectural decision, including conceptual alternatives, with clear evidence status. Human approval required for architecture-impacting or privileged decisions.
5. Never run untrusted code on the control plane. Never publish tokens, logs with PII, screenshots with private information, or internal Notion-only information in public docs.

## Commands and environment

Requires .NET 10 SDK. `dotnet restore ArchPulse.sln --locked-mode`; `dotnet build ArchPulse.sln --no-restore -c Release`; `dotnet test ArchPulse.sln --no-build -c Release`. Use the canonical native validators: `dotnet run --project tooling/ArchPulse.RepositoryValidator` and `dotnet run --project tooling/ArchPulse.TraceabilityValidator`. For local NuGet metadata checks run `dotnet run --project tooling/ArchPulse.LicenseAudit -- --repo .`. Python governance scripts in scripts/ are legacy/optional, not the native core or primary CI. Do not infer license clearance or product completion from passing foundation checks.

## Start/resume checklist

1. Verify actual `git remote -v`, `git status --porcelain=v1`, `git rev-parse HEAD`, base branch and branch head. Inspect open PR, issue assignment, codeowners and current CI. Check current settings, not previous agent prose.
2. Read task Txxx, Sxx, requirement IDs, current acceptance tests and dependencies in Notion. Inspect newest handoff under `docs/operations/agent-handoff/` and reconcile claimed states against GitHub and real artifacts.
3. For uncertain external effects (PR created, comment posted, permission change, branch rules changed), **read actual remote state before retrying**; use idempotency keys and record the outcome.
4. Create a bounded branch; commit incremental code, tests and docs; record exact commands, exit codes, revision and artifact digests. Do not fake unit test, build, runtime or benchmark evidence.
5. Update task checkpoint with `completedSteps`, `unverifiedSteps`, `testResults`, `externalEffects`, `knownBlockers`, and `nextSafeAction`. Make claims immutable or append-only where possible; never store credentials.
6. Send PR for review. Do not merge or change approved architecture without required human authorization. Do not sign legal agreements for another individual.

## Handoff

See [handoff protocol](docs/operations/agent-handoff/README.md) and [schema](docs/operations/agent-handoff/handoff.schema.json). The file `FOUNDATION-2026-11-10.json` records what was observed **in this staging artifact** (not GitHub). An agent must verify repository creation before writing remote IDs, branch SHA or CI conclusions.

## Evidence audit and current handoff

The original foundation checkpoint is historical and contains formerly pending remote actions. Review the latest [foundation verification ledger](docs/operations/FOUNDATION-VERIFICATION.md) and [2026-11-10 audit handoff](docs/operations/agent-handoff/FOUNDATION-AUDIT-2026-11-10.json), then independently query the latest GitHub branch, ruleset and CI before changing anything. Also read the [CI gate policy](docs/operations/CI-GATES.md) and unresolved [name/branding risk](docs/operations/BRANDING-RISK.md).

## Rebuild branch handoff (2026-11-12)

The initial .NET-native work was integrated into main via PR #30 and stacked PRs #32–#35; historical rebuild branches are not current source-of-truth.
See docs/operations/REBUILD-2026-11-12.md,
docs/operations/agent-handoff/REBUILD-2026-11-12.md and
docs/guides/dotnet-native-cli.md. Never claim proposed S00–S33
requirements are verified merely from foundation CI or the limited first
Roslyn graph. Preserve existing agent work/PRs and do not rewrite main.

## Experimental graph indexing milestone — 2026-11-12

The rebuild branch also contains a real MSBuildWorkspace solution
indexer, immutable JSON graph snapshots, evidence-scoped queries and
preliminary ArchitectureGuardian rules. Read the .NET CLI guide and
proposed ADR-0001. NEVER load untrusted MSBuild projects on the shared
VPS. The waiver approver text is not verified identity and cannot
authorize a protected change. S02/S03/S04/S15 remain incomplete.

## Read-only local API milestone

An experimental ASP.NET Core server exposes stored Graph queries on
127.0.0.1:5217. See docs/guides/local-graph-api.md. The server is
unauthenticated and must NEVER be publicly exposed. It cannot execute
code or commit changes. Native C# MCP Apps and React Studio prototypes are now merged, but real ChatGPT host verification, identity and production API authorization remain incomplete.

## Experimental React Web Studio

The integrated Web Studio prototype lives in apps/web-studio. It only loads
source-backed graph JSON by exact project and revision from the
loopback-only ASP.NET API and never makes up nodes or traces.
Do not treat it as a fully accepted S09/S27 studio or remotely expose
its unauthenticated local servers. See apps/web-studio/README.md.

## Native .NET MCP Apps experiment

The merged experimental native MCP implementation contains server/ArchPulse.McpServer
(5 read-only tools plus ui://archpulse/graph). Never expose or tunnel
the unauthenticated 127.0.0.1:5231 endpoint. Do not claim that tests
in an MCP Apps host simulator demonstrate ChatGPT product integration.
See docs/guides/mcp-apps-local.md. Full S29/S30 acceptance pending.

## Native results-only Test Journey evidence (S31/S33 partial)

The offline C# TRX importer in tooling/ArchPulse.TestJourney reads existing trusted local test reports and emits result-only evidence with SHA-256. It does NOT execute tests, observe implementation calls, capture values or prove test-to-method mapping. See docs/guides/test-journey-trx-results.md. Run only in approved local/CI contexts; never expose code execution through unauthenticated MCP or HTTP APIs.

## Static method-to-test projection (S31/S33 partial)

`SolutionGraphIndexer.AnalyzeSolutionAsync` and `ArchPulse.Cli test-links` can project
framework-annotated test method declarations onto source-backed, directly invoked
target method SymbolIds using Roslyn evidence. This is **not runner-discovered
test-case identity, a passed test, per-test coverage, or an observed call trace**.
Keep TestCases out of the default architecture canvas. No web/MCP execution
endpoint may accept an untrusted project for this analysis. Read
`docs/guides/in-place-method-test-associations.md` and the separate TRX results guide.
