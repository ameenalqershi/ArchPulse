# T010/S02 — Graph journal JSON object integrity (2026-11-14)

## Scope and coordination

- T010 is In Progress; S02 remains Proposed. This is a narrow, reviewable data-integrity safeguard, not product acceptance.
- Feature branch: feat/t010-journal-json-integrity-20261114, isolated Linux .NET 10 checkout.
- Dependency now merged: PR #70 feature head b00472eb27c02d41b48974ba5d1ab1f779214839 (local checkpoint projection), integrated to main as ce1f051a860c3e892325d083992cbea446aad800. This branch was reconciled with that main tree without changing its tested source.
- PR #68 addresses a different S25 handoff validator; none of its files were edited. No other agent's checkout, branch or main was modified.

## Observed failure and completed change

- Before the fix on main, two xUnit regression cases FAILED because repeated JSON keys in an event or its nested GraphNode were silently accepted during replay; reserializing the last-value-wins object still matched the stored SHA-256 digest.
- The journal now recursively rejects duplicate object property names before deserializing committed events, checkpoint documents and checkpoint anchor events.
- Reader errors use fixed diagnostics without reflecting untrusted JSON property names. Existing stored canonical JSON remains compatible.
- Five focused xUnit cases cover top-level and nested event properties, top-level and nested checkpoint properties, and a duplicate-key anchor event.
- The guide records these checks and distinguishes them from tamper-proof or authenticated storage.

## Exact local verification (isolated branch based on published PR #70 head)

- dotnet restore ArchPulse.sln --locked-mode: PASS.
- dotnet build ArchPulse.sln -c Release --no-restore: PASS, 0 errors and 0 warnings.
- Focused xUnit selection for duplicate-event/checkpoint/anchor properties: 5/5 PASS.
- dotnet test ArchPulse.sln -c Release --no-build: 317/317 PASS across 8 test assemblies.
- Native RepositoryValidator --handoffs: PASS.
- Native TraceabilityValidator --baseline: PASS.
- Scoped dotnet format --verify-no-changes on changed C# files: PASS.
- git diff --check: PASS.
- GitHub hosted CI is NOT claimed by these local receipts. Verify checks against the final pushed SHA.

## Limitations and next safe action

- Input is trusted local-journal filesystem data. This does not defend against arbitrary local actors able to rewrite all events/digests, symlink/path races, filesystem power loss, untrusted code execution, multi-host concurrency or missing checkpoint authentication.
- No data migration, HTTP/MCP listener, runtime server, public endpoint or external credentials were changed.
- PR #70 is merged. Confirm latest main and the new PR state before retrying publication, and replay the full suite if source changes.
- Request human review and exact-SHA hosted CI. The parent PR #70 is merged; do not auto-merge or mark T010/S02 Verified without their remaining acceptance gates.
