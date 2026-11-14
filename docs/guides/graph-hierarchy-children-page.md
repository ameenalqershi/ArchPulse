# T011/S02/S27 — Canonical graph children pages (experimental)

ArchPulse stores a *single canonical parent* on every source-backed GraphNode
(`ParentSymbolId`). The new local-only read endpoint enumerates direct
children without conflating ownership with `Calls` or other cross-scope edges.

## Exact-revision read

```text
GET /api/graphs/{project}/{revision}/children-page?pageSize=25
GET /api/graphs/{project}/{revision}/children-page?parentSymbolId=EXACT_SYMBOL&pageSize=25
GET /api/graphs/{project}/{revision}/children-page?parentSymbolId=EXACT_SYMBOL&pageSize=25&afterSymbolId=LAST_CHILD
```

- Omit `parentSymbolId` to list canonical **roots** (`ParentSymbolId=null`),
  useful for opening a master architecture map.
- Supply an exact parent ID to retrieve **direct** child nodes only; the
  response never includes grandchildren merely because a call edge connects them.
- The JSON schema is `archpulse.graph-children-page/v1` with `schema`,
  `project`, `revision`, `parentSymbolId` (nullable), `pageSize`,
  `nodes`, `hasMore` and `nextAfterSymbolId` (nullable). Each returned
  GraphNode retains its existing `SourceEvidence` classification/location.
- Nodes are ordered by ordinal `SymbolId` regardless of storage order. A
  cursor is the prior page's last returned `SymbolId`, **exclusive**.
- Page size defaults to 50, allowed 1..100. Invalid/foreign cursors and
  parameters return 400; an unknown parent or exact revision returns 404.
  A leaf with no children returns 200 and an empty page.
- Clients must preserve the exact project, graph revision and parent scope
  for each consecutive page. The cursor is **not signed** or permissions-aware.

## Evidence, performance and boundaries

This is a bounded **response and selection-memory** projection, holding at
most `pageSize + 1` child candidates in a SortedSet. It scans/validates the
**entire stored graph** per call. It is NOT bounded graph I/O, a database
index, an authorization-scoped `ViewScope`, a verified business-feature
membership engine, a runtime call trace, or a scalable S27 studio by itself.
The method cannot infer canonical hierarchy that is absent from the existing
Source Evidence graph.

No mutating HTTP/MCP routes, untrusted MSBuild processing, permission bypass
or remote listener is added; localhost API remains unauthenticated and must
not be exposed to untrusted clients. Full T011/T119/T121/S02/S27 acceptance
requires approved hierarchy contracts, per-workspace authorization,
hierarchical query plans/load tests and integration with Studio drilldowns.

Reproduce `GraphHierarchyChildrenPageTests` and
`GraphHierarchyChildrenApiTests` for pagination order, wide graphs, exact
source evidence, root scopes, foreign cursors and HTTP 400/404/405 behavior.
