# T025/S05 — retained-identity structural impact signals (2026-11-14)

## Scope and ancestry

- Base tested integration parent: `76b8086dd28d6b27373b16dec77e9795da67336d`, merged into public `main` by PR #65 (main merge `98ecab4e2c0008d9ca1dca80a344e91a8fc76bb1`).
- This follows T011 `changedNodes`/`changedEdges` GraphDifference now in main and complements the already merged #62 revision-index performance change.
- Target T025/S05 remains **In Progress / Proposed**. This work is a bounded additive experimental API signal, **not** complete business-feature impact or GitHub PR patch classification.

## Implemented

- New `StructuralChangeKind` values `NodeModifiedBefore`, `NodeModifiedAfter`, `CallModifiedBefore`, `CallModifiedAfter` expose retained-ID structural/source-location changes instead of ignoring them.
- Emit source-backed changes on their exact FROM/TO graph side. Only `Static` or `Verified` source evidence is eligible, and only `Calls` relations can generate call-change events. An inferred side remains unreported instead of being promoted to static evidence.
- Retain deterministic ordinal ordering, exact revision-specific reverse call witnesses and cached indexes, 500 event cap, 10k candidate signals, client schema `archpulse.feature-impact/v1`, `RuntimeStatus=Unknown` and candidate-only membership.
- Same label/edge/location/source-revision-only differences do not imply changed behavior or runtime occurrence. GraphDifference still cannot observe body-only method edits preserving graph metadata.

## Evidence (local integrated branch checkout)

- SDK .NET 10.0.114, Linux VPS in isolated checkout.
- Locked solution restore: success.
- Release build: **0 errors / 0 warnings**.
- Focused FeatureImpactAnalyzerTests: **11/11 PASS**, including stable-ID node location/label and changed call destination provenance, inferred rejection.
- Focused in-memory ASP.NET FeatureImpactApiTests: **4/4 PASS**, including string-valued new change kinds, exact FROM/TO evidence and Unknown runtime status. Final full-suite total must be rerun after this additional HTTP test.
- Full .NET test solution after the new HTTP contract regression: **291/291 PASS**, zero failures (eight assemblies).
- Native RepositoryValidator and Traceability baseline: PASS.
- Scoped dotnet format verify for changed C# source/tests and `git diff --check`: PASS.
- Hosted CI and independent clean published-SHA run: must be checked after publishing PR; **not yet claimed** at this checkpoint.

## Security/compatibility and next action

- No external network listeners, files or privileged effect added.
- Additive enum values change the experimental v1 signal vocabulary. Clients that assume an exhaustive set need human compatibility review. Re-run tests and code analysis on exact published branch SHA, inspect hosted CI and merge under current GitHub rules if accepted.
- Cross-service/runtime change impact, source authenticity, dynamic data flows, Guardian integration, real Git/PR patch interpretation and production performance remain unimplemented.
- Do not set T025 Verified or S05 Approved from this narrow check.
