# T056/S11 — Experimental portable artifact evidence

This additive .NET 10 local-only prototype lets an agent write **pre-redacted
text artifacts**, bind their SHA-256 digests to an immutable versioned manifest,
and let a replacement agent re-check the bytes and metadata without the prior
agent's chat transcript. It extends the [T054 experimental checkpoint journal](portable-task-ledger.md)
and [T053 portable handoff data contract](portable-handoff-contracts.md).

## Offline flow

1. Preflight and redact source test logs, diffs, decision notes and receipts
   **before** sending them to the store. The store only heuristically detects
   a few common credential patterns and is **not** a secret scanner.
2. Create a validated T054 checkpoint for task `Tnnn` with a stable
   `EnvelopeSha256` and task ID. No artifact is accepted for an unknown task.
3. Store each artifact using `PutArtifact(taskId, opaqueName, kind,
   sourceCommit, redactedText)`. Supported kinds:
   `git-diff`, `test-report`, `build-log`, `policy-receipt`,
   `decision-record`. Each payload is UTF-8 text, no more than 128 KiB.
   Content is stored by SHA-256 under `<trustedRoot>/Tnnn/cas/`.
4. Call `PutManifest(taskId, baseCommit, headCommit, artifactReferences)`
   with 1–16 distinct, bounded references. Only references to the two
   commit IDs in the manifest are allowed. A manifest has
   `archpulse.artifact-manifest/v1`, task ID, envelope digest, exact
   base/head commit, and artifact list. It is serialized as bounded
   local JSON at `<trustedRoot>/Tnnn/manifests/<digest>.json`.
5. Add the resulting **manifest** digest as a `evidenceSha256` value in a
   later T054 checkpoint. This link is only content-addressed, not an
   authenticated receipt.
6. After interruption, the receiving agent gets the task ID, latest T054
   evidence digest, trusted store root and current repository revision.
   `ReadManifest(taskId, manifestDigest)` verifies the manifest SHA-256,
   strict schema, task/envelope and every referenced file's digest and
   length. `GetArtifact` returns verified text.

### Example

```csharp
var storage = new PortableArtifactEvidenceStore(trustedLocalRoot);
var result = storage.PutArtifact("T056", "tests", "test-report",
    headCommitSha40, "Passed: 3, Failed: 0 [sanitized fixture]");
var manifest = storage.PutManifest("T056", baseCommitSha40,
    headCommitSha40, [result]);
// Store manifest.Sha256 in the next T054 evidence checkpoint.
// The replacement agent can now call ReadManifest("T056", manifest.Sha256).
```

## Validation and fail-closed behavior

Missing or tampered blobs, unexpected schema versions, duplicate property
names (including nested properties), unknown properties, corrupt hashes,
symlinked leaf files, disallowed kinds, path-like names, out-of-scope commit
IDs, invalid manifest count and oversize files are rejected.
Immutable writes use unique pending files, flush the file and rename without
overwriting. Identical concurrent content writes are idempotent and
orphan pending files are not considered committed evidence.

**Important boundaries:** This is not an external object store, backup
policy, verified test-run engine or authentication mechanism. The source
commits, policy status and test outputs inside files are **claims only**;
the receiver must confirm Git objects, CI jobs, test commands, reviewers,
approvals and permissions separately. It does not independently inspect
a Git repo or prove that hashes correspond to the *actual* build execution.
File digest matches do not prove the file was produced by a trusted source.
A writer who can rewrite the entire store can forge new self-consistent
evidence. Directory metadata durability after power loss, hostile
filesystem symlink races, cross-host transactions, retention and
cryptographic signatures are outside this slice. Do not store secrets
even if the pattern check does not recognize them.

T056 still depends on formal T054/T053/T008 decisions and security review;
it remains **In Progress**, not Verified. No MCP or HTTP API is exposed.
