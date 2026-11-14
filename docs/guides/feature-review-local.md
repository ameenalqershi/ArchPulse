# FeatureAtlas candidate review — T026/S05 partial

**Experimental on 2026-11-13.** [T026 in Notion](https://app.notion.com/p/4143830ba34d816fa950d2a0baaced88)
is Planned and S05 remains Proposed. This implementation is a **trusted-local
operator-declared review journal**, not an authenticated human workflow,
cryptographic signature, approved source of business truth, multi-tenant RBAC or
production UI.

## Trust and provenance boundaries

- An immutable T023 `FeatureCatalog` contains source-backed candidate member
  IDs and `Designed`/`Inferred` business mapping claims only.
- Every `feature-review-record` operation must run through the **local
  trusted CLI**, supply a JSON instruction, an explicit
  `--confirm-local-human-review` argument, `LocalHumanDeclared`, the exact
  expected review head, a bounded reviewer label and justification. This is
  **not identity authentication**. A machine operator could lie about its name.
- `OperatorApprovedCandidate` and `OperatorRejectedCandidate` are review
  dispositions only; they do **not** upgrade business membership to
  `EvidenceKind.Verified`, claim test results or alter a source graph.
- Requests declaring `Automation` cannot append or overwrite any disposition,
  even if they know the current review head. There is no AI tool or HTTP/MCP
  write path. This protects against normal automated producer updates,
  **not** against someone with direct filesystem access impersonating a human.
- Each successful operation creates one new immutable
  `000001.review-receipt.json`, `000002.review-receipt.json`, etc., in an
  exact graph-project/revision/**catalog SHA256** directory. Receipts have a
  monotonic sequence, `PreviousHash`, `Hash`, actor label, reason, timestamp
  and decision. SHA-256 verifies the internal chain against byte edits, but is
  **not a signed external attestation**; filesystem administrators can delete
  or rebuild the journal. Do not use it as nonrepudiable legal evidence.
- `Rollback` refers to the result after an **earlier receipt sequence**,
  including sequence `0` for the original unreviewed catalog. It creates a
  new receipt preserving all earlier history instead of deleting revisions.
- Optimistic concurrency requires the caller to supply current `ExpectedHead`.
  The next sequence file is atomically created; simultaneous stale writes fail.
  This is single-host, trusted-local operation, not a distributed consensus
  protocol or robust crash recovery across hardware failures.

## Reproduce safely

Only process **trusted** .NET solutions with MSBuild. The following
deterministic script operates on a synthetic fixture and uses a clearly marked
unverified reviewer label:

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/billing-multi-project/Billing.sln --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release
bash scripts/ci-feature-review.sh
```

To work on a trusted existing graph/candidate feature catalog:

```bash
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-review-show Billing --revision <exact-revision> --store /path/to/trusted-store
```

From the read result, copy **Head** and the exact member `SymbolId`.
In a local file accessible only to the operator:

```json
{
  "action": "ApproveCandidate",
  "actorKind": "LocalHumanDeclared",
  "actor": "Operator-supplied label, not authenticated",
  "reason": "Reviewed the proposed mapping against trusted local source",
  "expectedHead": "GENESIS",
  "featureId": "create-invoice",
  "symbolId": "Billing::M:Some.Existing.Symbol"
}
```

`GENESIS` only applies when no receipt exists; replace it with the latest
`Head` after the first operation. The member and revision must exist, and
human confirmation is required:

```bash
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-review-record Billing --revision <exact-revision> \
  --store /path/to/trusted-store --request /path/to/review.json \
  --confirm-local-human-review
```

For a rollback, use `"action":"Rollback"`, provide
`"rollbackToSequence":0` (or another earlier sequence) and omit
`featureId` and `symbolId`. Supply the **latest** `expectedHead`.
Previous receipts remain visible. For corrections to symbol membership not
already in the immutable catalog, an operator must create a new source
revision/candidate catalog: the review journal cannot inject graph symbols.

Read-only loopback interfaces:
- `GET /api/graphs/{project}/{revision}/feature-reviews`;
- tenth local MCP tool `archpulse_feature_reviews`.

Both return `NotIndexed` for missing catalog; neither can write. They
reject tampered data, limit response sizes and make the unauthenticated
provenance explicit. Do **not** expose their unauthenticated 127.0.0.1 services
publicly.

## Outstanding for complete T026

Authenticated reviewer identity and signatures, audited external identity/
role and approval enforcement, full editable web UI, curator correction UX,
cross-revision migration and conflict resolution, secure key storage, durable
off-host checkpoints, independent tamper-evident seals, multi-agent permissions
and maintainer approval are **not implemented**. A successful local test or
receipt is not formal verification of T026, S05 or business behavior.

## Fail-closed JSON receipt representation — T026/S05 bounded hardening (2026-11-15)

Review receipts are now accepted on read only if their full stored UTF-8 JSON bytes
match the exact representation produced by the journal's versioned serializer.
This is intentionally stricter than rehashing a deserialized record: unknown
fields, duplicate keys, differently-cased aliases and whitespace/field-order
rewrites cannot be silently discarded and represented as the same reviewed
assertion. Existing receipts emitted by this journal are already serialized
in that canonical format. Operator request JSON remains formatting-flexible,
but repeated property names at any depth (including case aliases under the
web serializer's case-insensitive property matching) are rejected before
reading the declared action. This rejects ambiguous input; it does **not**
authenticate a claimed human, protect from administrators rewriting entire
histories, or certify business features. Backup older journals before changing
serializer options or migrating schemas: canonical receipt bytes are part of
the local v1 storage contract.
