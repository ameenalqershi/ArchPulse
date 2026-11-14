# T027/S06 — Retain proven raw sink input amid unrelated unknown arguments (2026-11-15)

## Scope and isolation
- Base main: c3da7c01de070c66b37716dd30f9990ae72afd04.
- Isolated clone/branch. Other agents' active PR #68/#71/#72/#73/#74/#75/#78/#79/#80 and main are untouched.
- Changes: existing local bounded Roslyn DataJourney classification, two regression fixtures and guide.

## Root cause and correction
- Old sink rule ordered any unknown input ahead of a separately proven raw input. A sink receiving both a marked raw value and an unrelated unknown parameter was demoted to Unknown; its most meaningful proven source witness could also come from a different declared sanitizer argument.
- New rule classifies a raw candidate as StaticPossibleRisk when other unknown arguments are only simple local/parameter reads without expression side effects.
- If an unknown argument is a call or otherwise unsupported expression, or previous control-flow was unsupported, the result stays Unknown.
- When risk is present, provenance SourceSymbolId comes from the raw argument, not a differently sourced declared sanitizer.
- No new evidence-kind claims; no AI promotion or runtime verification; original API/report schema unchanged.

## Reproducibility
- Focused negative test failed before correction, and separate uncertainty-preservation test passed before correction.
- Both focused tests and complete LocalDataJourneyTests suite 10/10 passed after correction.
- Run locked solution restore, Release build, whole .NET solution tests, repository and traceability validators and scoped formatting. Record exact numbers in PR body after execution.
- Verify published SHA from a separate clean clone and GitHub CI on that exact SHA.

## Known limits
- This is straight-line, annotated, local source analysis only. No proof about interprocedural effects, uninstrumented runtime calls, source/sink annotations being honest, safety or confidentiality.
- T027 In Progress and S06 Proposed. Requires human engineering review; no autonomous merge.
