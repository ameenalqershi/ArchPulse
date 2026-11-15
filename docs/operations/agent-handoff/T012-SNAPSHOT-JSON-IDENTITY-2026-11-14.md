# T012/S02 — Reject ambiguous persisted graph snapshots (2026-11-14)

## Isolated scope and source
- Base main: ce1f051a860c3e892325d083992cbea446aad800.
- Task: T012 (In Progress); S02 remains Proposed. Distinct from pending T010 GraphRevisionJournal PR #72, T104 license PR #73, S25 PR #68, and S11 PR #71.
- Isolated Linux VPS clone and independent feature branch; main and other agents' worktrees were not modified.
- Files: GraphSnapshotStore.Load, Roslyn source-backed store tests, local ASP.NET graph API regression tests, local graph API documentation.

## Reproduced issue
- 4/4 new source-store regressions failed against unchanged main: top-level project identity, node label, nested evidence revision, and edge relation duplicate JSON properties were silently accepted by last-value-wins deserialization.
- This is an ambiguity/integrity risk, not evidence that a malicious actor modified production files. No remote HTTP write path was used.

## Bounded fix
- Parse the stored JSON document, recursively reject duplicate object-property names before deserialization, and preserve the existing JsonException public contract for malformed input.
- Return InvalidDataException for duplicate properties; existing local API exception mapping responds with sanitized HTTP 422. No source paths, property names, or injected marker are reflected in public responses.
- Add two ASP.NET in-memory regression cases for duplicate root/evidence properties, plus preservation of valid/missing versions and route behavior.

## Local reproducibility
- dotnet restore ArchPulse.sln --locked-mode: PASS.
- dotnet build ArchPulse.sln -c Release --no-restore: PASS, 0 errors/warnings.
- dotnet test ArchPulse.sln -c Release --no-build: PASS, 318/318 in 8 assemblies.
- New four focused store regressions: failed before the change, passed after; selected GraphSnapshotStoreTests 23/23 PASS and API corruption theory 6/6 PASS.
- Native RepositoryValidator --root . and TraceabilityValidator --baseline: PASS.
- Scoped dotnet format whitespace --verify-no-changes and git diff --check: PASS.
- Hosted GitHub CI on final published SHA has not been observed at the time of this local receipt; check it before merge.

## Limits / open gates
- No cryptographic signature, trusted source attestation, authenticated provenance, bounded-memory JSON validation benchmark, or production-grade object store.
- Existing immutable snapshot path naming and on-disk format are unchanged. Reader parses a JSON DOM before object materialization; large graphs need benchmark review.
- Existing trusted local API remains unauthenticated loopback-only; never expose it to a public network.
- Human engineering review required. T012/S02 are NOT Verified/Approved; parent T010 dependencies and larger provenance contract remain open.
- Next safe step: inspect exact published SHA, PR review comments and 8 hosted CI checks, and only then consider merging after authorized review.
