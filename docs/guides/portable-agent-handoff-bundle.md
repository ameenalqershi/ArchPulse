# S11/T057 — Provider-neutral local handoff bundle (experimental)

This is a **data-only offline handoff proof of concept**, not a connected AI-agent coordinator.
It composes T053 task envelopes, T054 immutable task checkpoints and T056 content-addressed
artifact manifests. T055 local lease claims remain separate and **are not serialized**;
a receiving agent must acquire its own permission and fresh lease independently.

The C# `PortableAgentHandoffJson.Create(trustedRoot, taskId, envelopeJson,
manifestDigest, nextSteps)` validates the canonical T053 task envelope, latest T054
checkpoint and manifest digest anchored in its `EvidenceSha256` array.
It returns versioned, bounded JSON containing task intent, current claimed status,
unknown external-effect IDs, checkpoint identity, evidence reference and the
human-authored next steps.

`PortableAgentHandoffJson.Read(trustedRoot, json)` validates the same evidence
at the destination. It rejects:
- Stale checkpoint sequence/digest after additional local task mutations
- Missing, changed or unanchored manifest/artifact data
- Unknown JSON properties/versions, duplicate JSON keys, secret-shaped strings
- Identity mismatches, invalid or duplicate unknown effect IDs and oversized steps
- Inconsistent or noncanonical T053 envelope encoding

A bundle never claims `CompletedVerified`. Unresolved external effects are explicit
and **must be reconciled before retrying privileged or non-idempotent actions**.
The two stub consumers in the xUnit tests represent two independent clients parsing
the same standard data, **not a proof of Codex↔other model integration**.

## Boundary and nonclaims

The bundle carries references to local evidence, **not the artifact bytes**. The receiver
requires access to the same trusted store and verified Git repository; copying
the bundle alone across hosts is insufficient. Stored SHA-256 provides consistency
but not authenticity of a build, CI run, source commit, approval or user identity.
The store and task journal remain vulnerable to trusted-root rollback and unprotected
writer bypasses. No distributed lease, credentialed transport, upload/download,
authentication, human authorization or exactly-once external mutation exists.
An AI agent should never use this object as a security capability or an instruction
to run commands without independent policy and human approval. Cross-provider
agent acceptance testing, linked storage integration and architecture review are
still mandatory before T057 is Verified.

Reproduce: `dotnet restore ArchPulse.sln --locked-mode`,
`dotnet build ArchPulse.sln -c Release --no-restore`, and
`dotnet test ArchPulse.sln -c Release --no-build`.
