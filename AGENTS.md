# ArchPulse — instructions for human and AI development agents

**Read first:** [README](README.md) → [S00](https://app.notion.com/p/4143830ba34d81e9b337d04a89706acc) → [S01](https://app.notion.com/p/4143830ba34d814fa155d2870b8e6c26) → [S25](https://app.notion.com/p/4143830ba34d81f3a119d6a5f7c01ae4) → selected Txxx and its Sxx → relevant S11/S12/S16/S17/S20. Do not rely on a previous conversation memory.

## Canonical truths

1. Notion is canonical for **planned scope, specifications and initial tasks**. Code and repo history are canonical for **implemented changes**; CI and signed artifacts for observed verification. A proposed spec is not a shipped feature.
2. Keep `Designed`, `Static`, `Observed`, `Inferred`, `Verified`, and `Unknown` separate. Never convert Unknown to Verified without independent evidence.
3. The .NET engine is intended to be local-first and independent of editor and AI provider; adapters must not duplicate source analyzers. Architectural modules have explicit boundaries; changes need a reviewable ADR when cross-module.
4. Mermaid source is mandatory for **every** candidate architectural decision, including conceptual alternatives, with clear evidence status. Human approval required for architecture-impacting or privileged decisions.
5. Never run untrusted code on the control plane. Never publish tokens, logs with PII, screenshots with private information, or internal Notion-only information in public docs.

## Commands and environment

Requires .NET 10 SDK. `dotnet restore ArchPulse.sln`; `python3 scripts/audit_nuget_licenses.py` (offline NuGet metadata change gate; see `docs/operations/NUGET-LICENSE-AUDIT.md`); `dotnet build ArchPulse.sln --no-restore -c Release`; `dotnet test ArchPulse.sln --no-build -c Release`. These commands are unverified until a real SDK run is recorded. Do not add fake projects for unimplemented modules.

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
