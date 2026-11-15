# T104/S19 — NuGet policy and lockfile JSON integrity — 2026-11-14

## Identity and isolation

- Task: T104; specification: S19 (Proposed); dependency approvals T004/T080 remain unresolved.
- Base main SHA: ce1f051a860c3e892325d083992cbea446aad800.
- Change scope: native offline ArchPulse.LicenseAudit input validation, deterministic xUnit regressions, local guidance. No graph, handoff, web/MCP API, or privileged repo settings changed.
- Separate clone/feature branch used to avoid concurrent agents' existing trees. Final published commit SHA and hosted CI must be obtained from actual PR, not inferred from this document.

## Reproduced defect and repair

NuGet license-policy JSON was deserialized with a case-insensitive last-value-wins reader; NuGet lockfiles were parsed without duplicate-key rejection. Four focused tests demonstrated that repeated properties (including nested resolved and differently cased policy names) silently passed and yielded a successful audit despite ambiguous input.

Both inputs now receive recursive object-property uniqueness verification before semantic reads. Policy keys are compared case-insensitively (matching JsonSerializerOptions); lockfile JSON keys use ordinal comparisons. Diagnostics do not include untrusted property names. The scanner remains local and does not follow external license URLs.

## Local evidence (authorized Linux VPS, .NET 10)

- Locked dotnet restore ArchPulse.sln --locked-mode: PASS.
- Before fix: 4/4 focused cases failed due to missing expected InvalidDataException (reproduced).
- After fix: 4/4 focused cases PASS.
- dotnet build ArchPulse.sln -c Release --no-restore: PASS, 0 errors/warnings.
- dotnet test ArchPulse.sln -c Release --no-build: PASS, 316/316 in 8 assemblies.
- Native RepositoryValidator --root . and TraceabilityValidator --baseline: PASS.
- Local LicenseAudit --repo .: 100 packages scanned, 1 pending legacy review, 0 audit failures. NOT legal clearance.
- Scoped dotnet format whitespace --verify-no-changes, and git diff --check: PASS after limited whitespace cleanup in the two touched C# files.
- Verify hosted GitHub CI on exact final PR head separately. No hosted CI claim is made here.

## Boundaries and next safe action

- Audit remains an offline package-metadata change gate. It does not attest package authenticity, legal compatibility, npm/actions/assets, or trademark clearance.
- One legacy URL-only license review still needs version-specific human verification. --require-complete is not claimed green.
- Do not call T104 or S19 Verified/Approved from these tests. Human review of PR and outstanding license/NOTICE obligations remains required.
- Next: fetch final published SHA and hosted checks, inspect code review comments, reconcile conflicting work without rewriting another agent's branches, and update Notion T104 with exact evidence.
