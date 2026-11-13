# Bounded source-graph hierarchy validation — T017 / S02 partial

The current native `GraphSnapshotStore.Validate` checks that every canonical
node belongs to a single existing parent, or a root, and that the ownership
parent chain contains **no cycles**. Ownership is separate from static `Calls`
edges, FeatureAtlas membership, and runtime graphs; those may legitimately
contain cycles and are not treated as ownership.

## Scalability correction (2026-11-13)

Earlier validation walked every node's entire chain to the root with a new
set each time. For a long chain of `N` nodes, this took
`1 + 2 + ... + N` parent visits — quadratic work and high allocation
pressure on large repositories.

The validator now uses two sets for the canonical ownership forest:
`visiting` for the currently scanned chain and `resolved` for all parent
chains previously proved to be acyclic. After a chain terminates at a root
or a resolved ancestor, all members are marked resolved. If a chain reaches
a still-visiting symbol, it fails closed as a cycle. No recursion or
source-project execution is involved.

Each node enters the current chain at most once and is resolved once:
**O(nodes + parent links)** traversal time and **O(nodes)** additional
memory in the parent validation phase. This is an algorithmic bound, **not**
an end-to-end performance measurement of indexing, graph diff, UI navigation
or storage throughput.

## Deterministic regression corpus

`GraphSnapshotStoreTests` adds:

- 30,000-node deep chain in reverse order — accepted without recursion
- 18,000-node cycle — rejected without recursion
- 10,000-node forest with 100 roots — accepted without false positives

To reproduce on a trusted checkout:

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test tests/ArchPulse.Roslyn.Tests/ArchPulse.Roslyn.Tests.csproj \
  -c Release --no-build --filter FullyQualifiedName~GraphSnapshotStoreTests
dotnet test ArchPulse.sln -c Release --no-build
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root .
```

The regressions check semantic correctness under large hierarchies. They do
not define production latency/SLO guarantees or represent the 100k+ symbol
and million-edge acceptance corpus requested by S02/T017. T015 incremental
indexing, T011 bounded pagination/queries, benchmarks and architectural review
remain separate, unfinished work.
