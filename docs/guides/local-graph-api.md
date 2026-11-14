# Local Graph Read API — experimental

This is an implemented read-only ASP.NET Core interface for real stored
ArchPulse graph revisions. It is **not** a complete S14 API or an
authenticated production deployment.

## Supported scope

- GET /health: reports experimental local-only service status; productionReady=false.
- GET /api/graphs/{project}/{revision}: returns a versioned, evidence-backed stored graph.
- GET /api/graphs/{project}/{revision}/neighbors?symbolId=...: exact symbol relationships.
- GET /api/graphs/{project}/{revision}/path?from=...&to=...: bounded shortest sourced path.
- GET /api/graphs/{project}/diff?from=...&to=...: compares immutable revision snapshots.
- GET /api/graphs/{project}/{revision}/datajourney-local: bounded static candidate source/sink and return **metadata** from T027, not observed values or security proof ([guide](datajourney-local.md)).
- GET /api/graphs/{project}/{revision}/feature-reviews: read-only local operator-declared hash-chained candidate review history, not authenticated or Verified ([guide](feature-review-local.md)).
- GET /api/graphs/{project}/feature-impact?from=...&to=...: read-only two-revision structural candidate impact; not a complete Git PR or runtime behavior analysis ([guide](feature-impact-local.md)).
- GET /api/graphs/{project}/{revision}/feature-signals: separately indexed, read-only T024 source candidates; `NotIndexed` if absent, HTTP 422 if invalid, no automatic business membership ([guide](feature-discovery-local.md)).
- GET /api/graphs/{project}/{revision}/features: exact manually curated FeatureAtlas catalog, or explicit `NotIndexed` when absent (T023/S05 partial).
- GET /api/graphs/{project}/{revision}/features/{featureId}: read-only candidate scope over exact graph node IDs and existing static edges; 404 for unknown feature, 422 for invalid stored data. [Details](feature-atlas-local.md).
- POST /api/guardian/evaluate: read-only evaluation of up to 100 client-supplied rules
  against an existing graph. No rule or graph is committed or applied to Git.

Missing revisions and symbols return 404, and there are no graph write or
code execution endpoints. Server uses System.Text.Json enums as strings.
xUnit in-memory HTTP contract tests and one real loopback smoke test
cover its initial behavior. The server never fabricates graph contents.

## Local-only startup

The first version binds to http://127.0.0.1:5217, has NO remote authentication
and is not suitable for public exposure. Never publish this port or
proxy it to the internet before implementing authentication, authorization,
TLS, audit and request/data handling review.

~~~bash
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
# Make a snapshot on a TRUSTED restored source checkout
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- index-solution examples/billing-multi-project/Billing.sln --revision golden-test --store /tmp/archpulse-graph
# Server may only read the configured local graph store
ARCHPULSE_GRAPH_STORE=/tmp/archpulse-graph \
  dotnet run --project server/ArchPulse.Server -c Release --no-build
~~~

From the same authorized machine, open the local health endpoint and
GET /api/graphs/Billing/golden-test. Do not mislabel this fixture as
a real user code graph. Avoid retaining sensitive local graph snapshots
beyond the local testing purpose.

## Security and engineering boundaries

- Graph contents and source locations may be sensitive: the current
  no-auth API is intentionally loopback-only and not production ready.
- The server does not evaluate MSBuild files. Indexing occurs only from
  the separately invoked trusted local CLI. This separates untrusted
  network input from arbitrary project evaluation.
- Request bodies are limited to 128 KiB on Kestrel, and Guardian accepts
  no more than 100 supplied rules. There is no abuse protection or
  per-user authorization; remote use is unsupported.
- The policy waiver model remains an unauthenticated experimental data
  contract. This API does not expose a waiver-approval mutation route.
- Future Studio/MCP integration must authenticate caller and symbol
  selection, enforce workspace boundaries, and avoid treating
  'Static' evidence as observed runtime behavior.

## Bounded Graph neighbor pages (T011 / S02 partial, 2026-11-14)

The original `/neighbors` endpoint remains unchanged for existing local consumers.
For callers requiring predictable response counts, use the additive read-only API:

```
GET /api/graphs/{project}/{revision}/neighbors-page?symbolId=EXACT_SYMBOL&pageSize=25
GET /api/graphs/{project}/{revision}/neighbors-page?symbolId=EXACT_SYMBOL&pageSize=25&afterEdgeId=LAST_EDGE_ID
```

A successful response is the JSON contract `archpulse.graph-neighbors-page/v1`
with `schema`, `project`, `revision`, `symbolId`, `pageSize`,
`edges` (exact source-backed `GraphEdge` records), `hasMore` and
`nextAfterEdgeId`. Edges are ordered by ordinal exact `EdgeId`, no
sort-order guessing; the cursor is the last returned EdgeId, **exclusive**.
A missing or nonincident cursor returns HTTP 400, an unknown exact method/
node returns HTTP 404, and an empty page after the final edge is valid.
`pageSize` defaults to 50 and is restricted to 1..100. No arbitrary
query expressions or code execution are accepted. Clients must preserve
**project, revision, and symbol** across page requests; this simple exact
EdgeId cursor is not signed and must not be treated as a revision-bound,
authenticated continuation token.

At most `pageSize+1` incident edges are temporarily held in the top-k
selection set, independent of degree. Each page still loads and validates
the **entire** local immutable graph and scans its edges: this is **bounded
returned edge count and selection memory**, **not bounded store I/O**, an
indexed/permission-scoped query engine, a limit on arbitrary serialized
string sizes, or full S02/T011/T121 acceptance. The legacy full graph and
neighbors responses remain unbounded and experimental. This endpoint is
localhost-only and unauthenticated; do not expose externally.

`GraphSnapshotStoreTests` cover stable order, multi-page completeness,
non-incident or fabricated cursors and unsafe page limits. `LocalGraphApiTests`
exercise the actual HTTP schema, 400 and 404 failure responses. All static
evidence retains its original kind; none is upgraded to runtime observations.
