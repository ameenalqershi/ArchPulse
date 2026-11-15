# S25 — reject ambiguous JSON object properties in public handoff records

**Status:** bounded implementation in review; not an accepted S11 runtime feature.
**Base Git SHA:** `75c7b3ddc13c536f8abe4cd346ee8136c1d19b88`.
**Branch:** `fix/s25-handoff-duplicate-json-20261114`.
**Source:** [S25 development-agent playbook](https://app.notion.com/p/4143830ba34d81f3a119d6a5f7c01ae4); supports but does not complete T117 or T053.

## Problem and change

`System.Text.Json` can parse repeated property names while consumers choose
which occurrence they trust. The public handoff validator previously checked
properties as a set, potentially treating contradictory `status` or nested
`externalEffects.state` fields as unambiguous. The validator now rejects
repeated object keys at every nesting depth, for checkpoints **and** their
schema, before processing historical content-based exception rules. Diagnostic
messages intentionally exclude untrusted property names.

## Reproduction on the isolated trusted clone

- `dotnet restore ArchPulse.sln --locked-mode`: PASS.
- `dotnet build ArchPulse.sln -c Release --no-restore`: PASS, 0 errors and 0 warnings.
- `dotnet test ArchPulse.sln -c Release --no-build`: PASS, 295 tests across eight .NET test assemblies, 0 failures.
- `dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root . --handoffs`: PASS.
- `dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build -- --root . --baseline`: PASS.
- Four focused new regression tests: PASS, including duplicate top-level state, nested external-effect state, schema key and unaffected valid records.
- `git diff --check`: PASS before publication.
- Existing `HandoffAllChecker.cs` has widespread whitespace violations outside the changed region; repository-wide scoped `dotnet format --verify-no-changes` fails for this pre-existing legacy formatting. Do not hide this CI-quality limitation; avoid a whole-file reformat in a security-only PR.

## Concurrency / limitations / next safe action

A separate repository checkout and a new branch were used; no shared worktree,
`main`, active agent branch, credential or production endpoint was modified.
This is offline structural validation, **not** a task lease, fencing token,
authenticated approval, actual external-effect reconciliation, cross-agent
exactly-once execution, or proof that two agents cannot conflict.

Next: inspect hosted CI on **the final PR head SHA**, obtain independent review,
and integrate only after normal policy gates. Keep T117/T053 not Verified until
their architecture dependencies and conformance criteria are satisfied.
