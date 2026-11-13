# T026 — local operator FeatureAtlas review ledger (2026-11-13)

## Scope and authorization

- Parent of this stacked change: [GitHub PR #48](https://github.com/ameenalqershi/ArchPulse/pull/48)
  at SHA `2fd02b62c37f7c5121e71231156bf7f184578b4d`.
- Its parents #47 (T024) and #46 (T023) are still unmerged.
- New branch: `feat/s05-feature-review-t026-20261113`.
- Isolated checkout: `/root/archpulse-t026-20261113`; no modifications
  to other worktrees or active agent branches.
- Authority: [Notion T026](https://app.notion.com/p/4143830ba34d816fa950d2a0baaced88),
  S05. Notion status remains Planned; this is only a **partial local slice**,
  not `Verified`, `HumanVerified` or a Production-gate acceptance.

## Implemented

- `engine/ArchPulse.Graph/FeatureReviewJournal.cs`: append-only
  SHA256 receipt chain with optimistic `ExpectedHead` concurrency,
  exact graph/catalog digest identity, local human-declared Approve/Reject
  dispositions and append-only rollback to earlier sequence, bounded receipts
  and validation on replay. Source/candidate claims NEVER become
  `EvidenceKind.Verified`.
- Reject `Automation` actors; the API and MCP have no writer. **ActorKind
  is merely operator-provided data, NOT authenticated identity**. Untrusted
  code with local filesystem access can impersonate a reviewer or delete a
  history directory: this system is NOT an external trust anchor.
- CLI `feature-review-show` and `feature-review-record --request ...
  --confirm-local-human-review` for trusted operators. No UI editing.
- Local read-only API `GET /api/graphs/{project}/{revision}/feature-reviews`
  and tenth read-only MCP `archpulse_feature_reviews`.
- Real fixture CLI smoke `scripts/ci-feature-review.sh`: synthetic reviewer,
  confirmed approval, automated overwrite rejection, rollback to sequence 0,
  full receipt history retained, altered text detected.
- Real .NET MCP protocol extended to ten tools; fixture review is clearly
  synthetic and never used as acceptance of a production business feature.
- Tests: xUnit journal transitions, failure modes, idempotency/concurrency
  heads, hash chain, revision/catalog mismatch, HTTP 404/422/405 and MCP
  read-only safety. [Operator guide](../../guides/feature-review-local.md).

## Required checks

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/billing-multi-project/Billing.sln --locked-mode
dotnet restore examples/feature-signals/FeatureSignals.sln --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release
dotnet test ArchPulse.sln --no-build -c Release
bash scripts/ci-feature-atlas.sh
bash scripts/ci-feature-discovery.sh
bash scripts/ci-feature-impact.sh
bash scripts/ci-feature-review.sh
bash scripts/ci-mcp-protocol.sh
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root .
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root . --handoffs
dotnet run --project tooling/ArchPulse.TraceabilityValidator --no-build -c Release -- --root . --baseline
dotnet run --project tooling/ArchPulse.RepositoryValidator --no-build -c Release -- --root . --adr
```

Revalidate hosted GitHub CI/ADR/CodeQL on **this exact PR head SHA**; local
tests do not substitute for hosted checks. Preserve branch protection and
unresolved security/admin issue #44.

## Nonclaims and next work

- Human reviewer identity remains unauthenticated, no signature or off-host
  audit checkpoint, no RBAC/TLS/tenant isolation, no secure web reviewer UI.
- Cannot rewrite or add candidate symbols in the immutable T023 catalog;
  instead create a new source/candidate revision. Revision migration and
  review carry-forward are unimplemented.
- SHA256 chain detects local receipt alteration when earlier files remain,
  but neither deletion of the entire journal nor reconstruction by an
  adversary with filesystem access can be proven without a trusted external
  seal. Do not claim legal nonrepudiation, identity proof, or tamperproofness.
- An approved *candidate* is not verified business behavior, observed
  execution, accepted T026 or accepted S05.
- Need authenticated operator workflow, permissioned UI, durable off-host
  append-only audit, signed identity, human source/feature corrections,
  workflow/rollback previews, and independent verification gates.
- Review/merge #46 then #47 then #48 then this PR; retain separate branch
  if parents remain open. No Python tooling, no paid AI API required.
