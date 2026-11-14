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

## Detecting retained-identity graph changes (T011/S02 partial, 2026-11-14)

`GET /api/graphs/{project}/diff?from=REV1&to=REV2` previously
reported only additions and removals of exact Node/Edge IDs. If the same
Symbol ID had a changed label, canonical parent, source file/line/column,
or provenance classification, it was not represented in the diff at all.
The same was true for an edge whose ID stayed stable while its endpoints,
relationship type or source evidence changed.

The `GraphDifference` response now also returns **`changedNodes`** and
**`changedEdges`**, sorted by ordinal ID. These new arrays are additive to
the existing `addedNodes`, `removedNodes`, `addedEdges` and
`removedEdges`, preserving existing consumer fields. The comparison
ignores **only the revision string inside SourceEvidence**: a new revision
with byte-equivalent structural declarations must not classify all records
as modified. It still detects any other source evidence location/kind
change. The implementation uses ID dictionaries and set operations rather
than repeatedly scanning the opposite revision.

This is a **static structural/source-location delta**, not an AST body diff.
Editing a method body while preserving its symbol identity, label,
containment, edge identities and source evidence locations can still yield
all six arrays empty; this does **not** mean the code or behavior was
unchanged, nor does it indicate runtime safety. It is also not a Git diff,
a provenance attestation, an event-sourced graph projection or a verified
business impact. `FeatureImpactAnalyzer` continues to use only
additions/removals and does **not** automatically promote
`changedNodes`/`changedEdges` to verified impacted features.

Regression tests exercise retained-identity label/location changes, edge
relation/evidence changes, parent moves, revision-only changes, deliberate
body-only blind spots and the read-only HTTP diff schema. Run
`dotnet restore ArchPulse.sln --locked-mode` followed by Release
build and `dotnet test ArchPulse.sln -c Release --no-build` on a trusted
checkout. S02/T011 acceptance and human review remain open.

## Fail-closed locally stored graph corruption — T012/S02 partial (2026-11-14)

The snapshot reader now rejects duplicate JSON object property names at any nesting depth **before** deserializing stored graph identity, nodes, edges and source evidence. Ambiguous files are rejected with a generic InvalidDataException (mapped to the existing sanitized 422 response on local graph routes); this is structural consistency checking, not a signature, authentication, or trusted provenance attestation.


A stored GraphSnapshot might have malformed JSON, a mismatched project/revision,
or invalid graph identity, parent relationship or provenance. On local read
routes, these cases now produce **HTTP 422 Unprocessable Entity** with a
generic Problem response rather than an unhandled HTTP 500 or a misleading
404. The raw JSON, source path, and inner validation exception are never
included in the HTTP response.

`GraphSnapshotStore.Load` keeps its original InvalidDataException and
System.Text.Json.JsonException contracts for CLI/MCP consumers. The ASP.NET
Core pipeline maps those two exceptions to a generic HTTP 422 only on the
local `/api/graphs` and `/api/guardian` routes, without exposing inner details.
Missing revisions still yield 404; intact revisions continue to return 200.
File-system I/O errors, access denials, and unrelated programming failures
remain different failures; they are not silently rewritten as bad graph data.
Read-only routes including graph, neighbors and revision diff share this
failure boundary; graph mutation or automatic repair is NOT introduced.

Regression tests deliberately tamper an isolated saved snapshot with invalid
JSON, forged project identity, invalid evidence source line and orphan edges.
Tests check response status and redaction, independent unaffected revision
readability, health status and missing-revision semantics. Tests are limited
to this in-memory server and fixtures; no deployment hardening, source attestation,
cryptographic content hashes, persisted index, filesystem safety audit, or
production S02/T012 verification is implied.
