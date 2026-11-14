# T119/S27 — Canonical scope topology (experimental)

Read exact, source-derived canonical ownership without treating calls, flows,
feature membership or unverified evidence as parent-child relationships.

## Local read-only API

```http
GET /api/graphs/{project}/{revision}/scope-topology?symbolId={exactSymbolId}
```

The `archpulse.graph-scope-topology/v1` response contains the exact `project`,
`revision`, `symbolId`, `rootSymbolId`, `depth`, the original source-backed
`node`, a root-to-selection `breadcrumb` array of original GraphNodes,
`directChildrenCount`, `descendantCount` and `directChildEvidence` as six
explicit Designed/Static/Observed/Inferred/Verified/Unknown category counts.

The canonical owner is only `ParentSymbolId`. Cross-scope Calls/DependsOn
edges neither add to descendant counts nor change breadcrumbs. Display-label
renames retain the same exact SymbolId path across source revisions; callers
must supply the **new revision** explicitly and never assume an old revision
has been promoted or updated in place. Multiple root scopes are supported.

A canonical graph is validated before projection (orphan, duplicate ID and
ownership-cycle rejection). Hierarchy building is iterative, with linear
O(nodes + edges) validation/projection and no recursive stack growth.
The projection holds child-index arrays and a descendant-count map.
The public in-memory `ChildrenPage` API supports exact-child binary-search
cursors and bounded pages (1–100), after building that index. It has no
persistent database index, no cross-request cache and does **not** reduce
the cost of reading an entire stored graph per HTTP request.

Breadcrumbs are bounded to 128 nodes. Unknown symbols return 404,
invalid/overlong IDs return 400 and over-budget breadcrumbs return 413.
Source evidence kinds are reported unchanged, never guessed/verified.

## Limits

The HTTP server remains unauthenticated local loopback; this endpoint is
informational, not an authorization-bearing ViewScope, nor permission or
source-hash attestation. The graph representation currently lacks separate
Assembly/Service/Module nodes; L0–L6 tests demonstrate depth and stable
identity, **not** that the full requested S27 business-level hierarchy has
been indexed. There is no recursive visual canvas or aggregate cross-scope
portal lens, and no verified runtime topologies or cross-host stores.

Do not expose the API over a public interface. T009/T011 are still partial
dependencies and S27 has no approved architecture decision; this is an
additive prototype, not T119 acceptance. See proposed ADR-0017.
