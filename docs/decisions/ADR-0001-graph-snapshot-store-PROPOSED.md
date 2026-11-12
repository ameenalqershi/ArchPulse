# ADR-0001 — Graph persistence options (PROPOSED)

- Status: Proposed; no human architectural approval
- Source: S02, S15, S17, S22, S26, S32
- Date: 2026-11-12

The interim requirement is a repeatable, local-first, source-proven
graph tied to the exact supplied revision. The following alternatives
are not yet compared in production workloads.

## Option A: Immutable atomic JSON (experimental implementation)

~~~mermaid
flowchart LR
    A["Roslyn Snapshot"] --> V["Validate identity and evidence"]
    V --> F["Write temp JSON and flush"]
    F --> R["Atomic rename by revision"]
    R --> Q["Local graph query"]
~~~

Advantages: inspectable, local-first, immutable fixture receipts.
Limitations: coarse writes, no indexed large-graph queries or
multi-host transaction support. Evidence: tests for idempotent write,
orphan relations, provenance, ownership cycles, revision diff.
Not production approval.

## Option B: SQLite versioned graph (not implemented)

~~~mermaid
flowchart LR
    A["Roslyn Delta"] --> T["SQLite WAL transaction"]
    T --> N["Revisioned nodes"]
    T --> E["Revisioned edges"]
    N --> Q["Indexed traversal"]
    E --> Q
~~~

Advantages: transactional local storage, indexed queries and compact
deltas. Costs: schema design, migrations, concurrent writer handling.
Risks: query scalability and changes to graph contracts. No benchmark
evidence or implementation yet.

## Option C: External graph database (not implemented)

~~~mermaid
flowchart LR
    A["Roslyn Graph"] --> B["Persistence adapter"]
    B --> D["External graph database"]
    D --> Q["Graph traversal"]
~~~

Advantages: graph-native querying at larger scale. Costs: independent
deployment and operations, permissions, services and more complex
offline use. Not tested; no performance comparisons exist.

## Pending decision

Keep the implemented JSON snapshot as an experimental test store
until workload measurements, fault recovery and security review
justify a production storage selection. Owner approval is mandatory.
