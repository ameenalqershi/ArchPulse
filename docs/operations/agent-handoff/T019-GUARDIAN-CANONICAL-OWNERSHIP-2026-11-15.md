# T019/S04 — architecture rule ownership provenance (2026-11-15)

- Base: main at 514d3fad1cb2bd6198a769888893505031b27fd5.
- Scope: existing experimental ForbiddenDependencyRule evaluator only. No changes to pending PR #68/#71/#72/#73/#74/#75 or recent #76.
- Reproduced: four new regressions fail before fix. Opaque IDs under valid parent projects are missed; misleading ID prefixes can claim violations under the wrong project; nested owners are missed; duplicate project labels are silently ambiguous.
- Fix: resolve owners through the validated canonical graph hierarchy using non-recursive memoization; require unique project labels for the current name-based rule selector. Missing/duplicate selector -> NotEvaluated, not Pass.
- Existing outcome semantics retained: Static/Observed/Verified -> Violation or bounded waiver; Inferred/Designed -> Unknown; zero matching edges -> NotEvaluated.
- Compatibility: existing rule/finding records and local API response schema remain unchanged. No waiver authority is added; no policy decision is auto-approved.
- Reproduction: dotnet restore ArchPulse.sln --locked-mode; dotnet build ArchPulse.sln -c Release --no-restore; dotnet test ArchPulse.sln -c Release --no-build; native RepositoryValidator/TraceabilityValidator; scoped dotnet format verification.
- Evidence is tied to exact published SHA in PR description; hosted CI and human review must be inspected after publishing.
- Limits: T019 In Progress; S04 Proposed; acceptance of T018 and T012 dependencies, runtime caller identity, authenticated evidence and whole-rule DSL remain open. Do not mark Verified or merge autonomously.
