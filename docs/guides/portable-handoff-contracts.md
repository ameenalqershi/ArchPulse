# Portable cross-agent handoff contracts — S11/T053 experimental slice

The .NET 10 types in `engine/ArchPulse.Core/PortableHandoffContracts.cs`
are **offline untrusted JSON data**, not a task coordinator, capability grant,
approval authority or confirmed external side effect. Canonical references:
[S11](https://app.notion.com/p/4143830ba34d811193dcd9c2937ae915),
[T053](https://app.notion.com/p/4143830ba34d813f832dc77c81b617e3),
and [S25](https://app.notion.com/p/4143830ba34d81f3a119d6a5f7c01ae4).

The [draft JSON Schema](../contracts/portable-handoff-v1.schema.json) documents all four shapes for non-.NET consumers; the native .NET reader additionally enforces duplicate-key, size, text, branch and credential preflight. Schema conformance alone is not permission or external proof.

## Interchange vocabulary

- `archpulse.task-envelope/v1`: task ID, goal, canonical repository URL,
  exact base commit, branch and opaque workspace alias, source requirement IDs,
  acceptance criteria, declared requirements and tool allowlist.
- `archpulse.capability-manifest/v1`: claimed agent identity and capabilities.
  **Not** an authenticated capability or permission grant.
- `archpulse.approval-receipt/v1`: decision bound to task/commit and claimed
  evidence reference. `Granted` is **not** an authenticated approval; the caller
  must independently check reviewer identity, policy, freshness and exact SHA.
- `archpulse.external-effect-receipt/v1`: action, idempotency key and
  `Planned`, `Succeeded`, `Failed` or `Unknown` outcome claim.
  `Unknown` must have a reconciliation step and no success digest.
  `Succeeded` requires a SHA-256 digest, which **does not prove** a remote
  effect actually happened. Reconcile remote state before retry.

Example **fictional** envelope, not an actual committed task:

```json
{
  "schema": "archpulse.task-envelope/v1",
  "taskId": "T053",
  "goal": "Reconstruct an offline task across two agents",
  "repository": "https://github.com/ameenalqershi/ArchPulse",
  "baseCommit": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "workingBranch": "feat/example",
  "workspaceAlias": "agent-b",
  "requirementIds": ["R-CONTINUITY-1"],
  "acceptanceCriteria": ["Another agent parses the same source revision"],
  "requiredCapabilities": ["git.read"],
  "allowedTools": ["git.read"]
}
```

Use `PortableHandoffJson.ReadTask`, `ReadCapabilities`, `ReadApproval`,
`ReadEffect` and `Write` for interchange; `Write` is **not** an
authorization or validation operation, and consumers must call the matching
`Read*` before trusting structural shape. Readers fail closed on unsupported
schema versions, duplicate JSON keys at all depths, absent/unexpected fields,
unsafe branch/workspace aliases, invalid SHAs, duplicate list items, known
credential signatures, invalid effect states and oversized input (64 KiB).
No original untrusted JSON text is echoed in exception messages.

## Boundary conditions and remaining acceptance

S11 and T053 remain incomplete. No implementation here provides:
durable ledger, recovery after a crash, CAS/version control, fencing token,
exclusive leases, source/digest cryptographic attestation, trusted identities,
approval expiry, effect reconciliation, signed artifacts, command execution,
distributed state, or independently verified agent takeover. This is a
small **portable contract prototype**, pending human review of T001/T002
architecture dependencies before contract stabilization. It is not connected
to any public API/MCP write endpoint.

Reproduce with `dotnet restore ArchPulse.sln --locked-mode`,
`dotnet build ArchPulse.sln -c Release --no-restore` and
`dotnet test ArchPulse.sln -c Release --no-build`. Focused:
`dotnet test tests/ArchPulse.Core.Tests/ArchPulse.Core.Tests.csproj --filter FullyQualifiedName~PortableHandoffContractTests`.
