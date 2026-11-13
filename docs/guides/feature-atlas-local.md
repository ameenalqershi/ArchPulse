# FeatureAtlas manual feature overlay — T023/S05 partial

**Status (2026-11-13): experimental, source-revision-bound C# implementation.**
The canonical [S05 specification](../requirements/specification-index.md) is still
**Proposed**. This implements an *explicit business feature catalog*, not automatic
feature discovery, human verification, acceptance enforcement or end-to-end DataJourney.

## Contract and its evidence boundaries

`archpulse.feature-catalog/v1` is a human-authored sidecar over an existing,
immutable GraphSnapshot. The catalog is keyed by exact `Project` and `Revision`,
and each member references an **existing** `GraphNode.SymbolId`. A feature does
**not** become that symbol's ownership parent. Multiple features can share nodes.

- Membership `claim` accepts only `Designed` or `Inferred`: these are manual
  proposals about *business meaning*, **not** proven mappings. Attempts to import
  `Static`, `Observed`, `Verified` or `Unknown` as membership claims fail.
- The returned `nodes` carry their own original Graph source evidence. Only
  existing Graph edges with `Static` or `Verified` provenance **between selected
  members** appear in `staticEdges`. Disconnected business steps remain
  disconnected; the engine does not invent cross-module calls, database writes
  or unindexed endpoints.
- `executionStatus` is always `Unknown`; `observedCoverage` is always false.
  No test execution, source-to-binary attestation or observed business flows
  are asserted by this catalog.
- `featureScope` and the catalog are **separate** from canonical ownership
  hierarchy and versioned Graph nodes. Review and governance/approval workflows
  are not implemented.

## Free, trusted local workflow

Use a restored, trusted .NET solution. **MSBuild project evaluation may execute
project-defined targets; do not index an untrusted checkout outside a sandbox.**

```bash
dotnet restore examples/billing-multi-project/Billing.sln --locked-mode
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  index-solution examples/billing-multi-project/Billing.sln \
  --revision my-exact-source-revision --store /tmp/archpulse-features

# Inspect source-backed Method SymbolIds in the index JSON first.
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  graph-show Billing --revision my-exact-source-revision --store /tmp/archpulse-features
```

Prepare a `feature.json` **outside the repository**, replacing both example
SymbolIds with **exact** values from the graph you just indexed:

```json
{
  "schema": "archpulse.feature-catalog/v1",
  "project": "Billing",
  "revision": "my-exact-source-revision",
  "features": [
    {
      "id": "create-invoice",
      "title": "Create Invoice",
      "description": "Proposed business scope pending review",
      "owner": "Example billing team",
      "criticality": "P0",
      "scenarios": ["An authorized user submits an invoice"],
      "acceptanceContracts": ["A reviewer must validate this mapping"],
      "members": [
        {
          "symbolId": "REPLACE_WITH_ACTUAL_METHOD_SYMBOL_ID",
          "reason": "Candidate endpoint or handler",
          "claim": "Designed"
        },
        {
          "symbolId": "REPLACE_WITH_ANOTHER_ACTUAL_METHOD_SYMBOL_ID",
          "reason": "Candidate downstream step",
          "claim": "Inferred"
        }
      ]
    }
  ]
}
```

```bash
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-import Billing --revision my-exact-source-revision \
  --store /tmp/archpulse-features --manifest /tmp/feature.json

dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-show Billing --revision my-exact-source-revision \
  --store /tmp/archpulse-features --id create-invoice
```

A different sidecar for the **same graph revision** is refused. To correct an
incorrect business definition, publish a new reviewed catalog revision with a
corresponding exact graph revision; editing existing sidecar bytes is not an
approved workflow. Treat output as local sensitive data, do not include
real customer names/PII/tokens, and delete disposable `/tmp` stores when done.

## Read-only client APIs

After starting the loopback-only Graph API with the same
`ARCHPULSE_GRAPH_STORE` directory:

- `GET /api/graphs/{project}/{revision}/features` reports `NotIndexed` when no
  catalog exists. This is *not* equivalent to a proven absence of features.
- `GET /api/graphs/{project}/{revision}/features/{featureId}` returns an
  exact manual scope or 404; corrupted catalogs return HTTP 422.
- Local MCP tool `archpulse_feature_scope(project, revision, featureId)`
  returns the same bounded, read-only source-backed scope with explicit
  candidate-mapping semantics. No remote MCP service or public authorization exists.

The server does not accept feature writes or evaluate MSBuild. Both experimental
servers are **unauthenticated and bound to 127.0.0.1 only**. Never expose them
through a tunnel or a public reverse proxy without identity, authorization,
TLS, workspace isolation, and a privacy/security review.

## Verification

The offline end-to-end test `bash scripts/ci-feature-atlas.sh` indexes the real
Billing two-project fixture, imports an operator-authored proposal targeting
an **actual call edge**, queries it, and rejects a nonexistent feature.
Unit/API/MCP tests additionally cover revision mismatch, duplicate/forged
members, claim elevation, corrupted storage, overlapping membership and
immutable re-import. The separate MCP protocol smoke exercises the seventh
tool through the official .NET MCP client.

**T024 partial:** a separate [offline Roslyn discovery guide](feature-discovery-local.md) describes candidate source signals from MVC attributes, named handlers and test declarations. Signals are always inferred business hints and **do not** populate this operator-curated catalog. **Not implemented:** full automatic discovery and human review (minimal APIs/OpenAPI/registration verification),
**T025 partial:** [source-graph structural candidate impact](feature-impact-local.md) can trace direct/reachable graph changes from a manual catalog, but not Git PR body patches, Guardian findings or verified behavior. Full T025, T026 authenticated reviewer and editable corrections ([partial local operator receipt journal](feature-review-local.md)),
feature-scoped UI navigation, business acceptance proofs, DataJourney
and observed feature-level Test Execution Journeys. No S05 or S27 full
acceptance is claimed.
