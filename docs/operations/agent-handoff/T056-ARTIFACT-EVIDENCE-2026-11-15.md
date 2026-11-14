# T056/S11 — bounded portable artifact and evidence manifest checkpoint

Date: 2026-11-15. Scope: experimental local artifact storage only.
Repository: `ameenalqershi/ArchPulse`, PR parent `#75` with exact
base SHA `caebcbd37d60257ce9cc4580d62c5e9c884c83c3`,
branch `feat/t056-artifact-manifest-v1-20261115`.
Isolated Linux VPS checkout `/opt/archpulse-t056-artifacts-20261115`.

## Changes made

- New `PortableArtifactEvidenceStore.cs` with strict
  `archpulse.artifact-manifest/v1` manifest: TaskId, EnvelopeSha256,
  exact base/head Git commit IDs and 1–16 scoped content-addressed references.
  Accepted kinds: git-diff, test-report, build-log, policy-receipt,
  decision-record. Each artifact is capped at 128 KiB of pre-redacted UTF-8.
- Identical content address `<root>/<Tnnn>/cas/<sha>.txt`,
  manifest `<root>/<Tnnn>/manifests/<sha>.json`.
  Creation requires a validated T054 checkpoint, duplicate/malformed
  manifest properties are rejected, source revisions must match manifest
  base/head, and read verifies every referenced file SHA and size.
  No overwrite; unique pending files; an orphan pending file is not evidence.
- Tests in `PortableArtifactEvidenceStoreTests.cs` validate restart,
  T054 evidence checkpoint linkage, tamper detection, missing artifact,
  nested duplicates with recomputed manifest SHA, revision mismatch,
  two concurrent idempotent writers, bound failures and symlinked files.
- Guide: `docs/guides/portable-artifact-evidence.md`.

## Gates

- Locked NuGet restore and Release build: PASS, 0 errors and 0 warnings.
- `dotnet test ArchPulse.sln -c Release --no-build`: **344/344 PASS**
  on 8 .NET test assemblies.
- Focused T056 xUnit: **14/14 PASS**.
- Scoped C# whitespace formatting and verification: PASS.
- Native RepositoryValidator and TraceabilityValidator baseline: PASS.

## Receiver warnings / next steps

T056 requires architecture approval of T054 and T008. It is **not Verified**.
The SHA-256 evidence proves local byte consistency only, NOT provenance,
actual Git/test execution, human approval, or remote effect success.
No write access to external tools, no authentication, no cross-host
transactions, durable directory fsync, backup policy or full secret DLP;
credential detection is heuristic only. A hostile store owner can rewrite
all objects and hashes. Verify exact published SHA, re-run hosted CI,
independently reproduce with a clean clone, and document review.
Do not merge this stacked PR before parent #75 (and #71).
