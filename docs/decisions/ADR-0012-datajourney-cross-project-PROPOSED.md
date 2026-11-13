# ADR-0012 — Static DataJourney through direct source calls

- Status: Proposed
- Date: 2026-11-13
- Scope: S06, T028, R-DATA-001 and R-DATA-002
- Review: Human architectural and privacy review pending

## Context and invariants

The local DataJourney report models explicitly annotated data within a method.
To inspect a value across C# projects, ArchPulse needs both evidence of the
actual call target and evidence that the tracked value participates in the call.
A call graph alone cannot supply the second. Reports contain source locations
and identifiers only, and runtime evidence requires an independent source.

## Option A — Follow directly bound source calls in Roslyn

~~~mermaid
flowchart LR
    Source["Annotated source"] --> Caller["Caller value"]
    Caller --> Call["Bound call and exact graph edge"]
    Call --> Callee["Callee argument or return"]
    Callee --> End["Annotated sink or explicit gap"]
~~~

Reuse existing semantic compilations and graph call edges in the CLI. Track
identity conversions of strings and follow bounded source-declared static calls.
Record unknown boundaries for unsupported behavior. This covers part of T028
while allowing review of the evidence model before any wider integration.

## Option B — Infer flows from the call graph alone

~~~mermaid
flowchart LR
    Entry["Caller method"] --> Graph["Graph call path"]
    Graph --> Destination["Destination method"]
~~~

This would be cheaper to query but could mistake a method call for value
propagation when a different argument or a constant reaches the sink. It
cannot substantiate named arguments, reassignment or return propagation.

## Proposed decision and acceptance

Try option A in the local CLI. Use the fixture tests to verify argument
binding, returned values, exact revision edges, opaque calls, controlled
negative cases, and omission of source literals. Review the trust model
before adding persistence, editor adapters, runtime joins or remote access.
The existing local report schema and canonical graph format remain compatible.
The additive CLI command can be removed by reverting its PR.
