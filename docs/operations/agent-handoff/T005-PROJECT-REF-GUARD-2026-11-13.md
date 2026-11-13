# T005 — static ProjectReference guard handoff (partial)

- Date: 2026-11-13; base main: `2e755869c672caf384c200106534f1e1cecab658`.
- Scope: T005/S01 structural project-reference acyclicity. **Partial**.
- Branch: `feat/t005-project-reference-guard-20261113`.
- No cross-module dependency was introduced; existing repository validator
  and existing tooling test project were extended.
- Verified initially: locked restore, Release build without warnings/errors,
  69/69 tooling tests (9 new project reference cases), native foundation checker.
- Follow-up patch on PR #57 adds root `.sln` membership checks and nine
  new positive/negative cases, with symlink-directory escape protection.
  Re-check full-suite and exact remote SHA before claiming this patch verified.
- No project files were executed during scanning; the tool parses XML with
  DTD processing prohibited.
- Remains unknown: correctness of conditional MSBuild references and all
  human-approved bounded-context policy. This parser fails closed on dynamic
  Include expressions rather than implying knowledge of resolved dependencies.
- Next step: validate the full .NET suite and hosted CI on exact published SHA,
  secure human architectural review, then extend actual S01 boundary policy
  only after the T002 decision is approved.
