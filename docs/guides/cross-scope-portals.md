# T120/S27 — Cross-scope portal projection (local-only prototype)

The canonical owner of a symbol is **only** `GraphNode.ParentSymbolId`.
Calls, dependency and feature memberships must not be used as parent links.
The exact-revision local graph API now exposes a bounded read-only preview of
original edges **crossing** the selected node's canonical subtree.

## HTTP API

```http
GET /api/graphs/{project}/{revision}/cross-scope-portals?scopeSymbolId={exactSymbolId}&pageSize=50
GET /api/graphs/{project}/{revision}/cross-scope-portals?scopeSymbolId={exactSymbolId}&pageSize=50&afterEdgeId={lastEdgeId}&maxScanEdges=100000
```

Returns `archpulse.graph-cross-scope-portals/v1` containing exact project,
revision, scope, `totalCrossScopeEdges`, `outboundCount`, `inboundCount`,
an evidence-kind histogram for the entire matching set, bounded
`portals`, `hasMore`, and `nextAfterEdgeId`.

For a canonical scope S, build its subtree from `ParentSymbolId`. An
original GraphEdge becomes a portal only when **exactly one** endpoint is
inside S. `Outbound` = from inside to outside; `Inbound` = from outside
to inside. Portals retain the original immutable GraphEdge (including exact
source evidence) and the two endpoint GraphNodes; no external node is
duplicated under the scope and no call edge is invented. Interior-to-interior
and exterior-to-exterior relations are intentionally excluded.

`pageSize` must be 1..100 (default 50); cursor is the previous page's
exact last EdgeId (exclusive). It is accepted only if it is a crossing
edge in the **same** exact revision and scope. A changed scope/revision
must start a new page series. The server returns 400 for bad parameters
or forged/non-boundary cursors, 404 for unknown exact revision or scope,
and 413 when a requested scan budget cannot cover all stored edges.

`maxScanEdges` defaults to 100000, hard maximum 1000000. Exceeding it
fails *before* projection rather than silently hiding a connection.
The count and evidence histogram cover **all** crossing edges, not merely
the current page. A graph with no crossing edges returns a 200 empty page.

## Truth and access boundaries

`accessStatus: "AuthorizationNotEvaluated"` is returned explicitly;
**no data-access permission has been checked or granted**. The endpoint is
available only on the existing localhost read-only API, and that API must
not be exposed to an untrusted remote client. Returning both endpoint
previews is acceptable only in the current single-user trusted local
context; a production ViewScope must filter data *before* materializing
or counting any external identities.

`featureMembershipStatus: "NotIndexed"` means this slice does NOT
derive cross-module business-feature membership. Ownership, contextual
membership and interaction remain distinct domains. Evidence categories
(Designed/Static/Observed/Inferred/Verified/Unknown) are copied from
original edges, with no automatic upgrade from Inferred to Verified.
Observed provenance on a fixture is a fixture, not a live OTLP trace.

The algorithm validates the full snapshot, builds the in-memory canonical
subtree and scans all edges for exact totals. It retains at most
`pageSize+1` selected edges in memory, but **does not bound total graph
I/O or provide persistent indexes, per-tenant access, cross-host durable
history, or a millions-of-edges performance benchmark**. Feature-context
mapping and Studio portal UX are future acceptance work.

Reproduce `CrossScopePortalQueryTests` and `CrossScopePortalApiTests`:
three-module crossing fixture, direction and source evidence, stable
pagination, cursor/budget failure cases, 12000-node/6000-edge functional
fixture, localhost HTTP 400/404/405/413 and no exposure of filesystem paths.
