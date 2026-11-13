# ADR-0014 — Revision-scoped DataJourney in Web Studio

- Status: Proposed
- Date: 2026-11-13
- Scope: T030, S06, S09
- Review: Human architectural and privacy review pending

## Context

The CLI derives source-only cross-project witnesses. Web Studio already has
a revision-scoped graph and a method inspector. The browser must retrieve
stored evidence without evaluating a project and must preserve Unknown
runtime status.

## Option A — Read a validated local report

~~~mermaid
flowchart LR
    CLI["Trusted CLI indexing"] --> Store["Revision-scoped sidecar"]
    Graph["Canonical graph"] --> API["Read-only loopback API"]
    Store --> API
    API --> Studio["Method inspector"]
~~~

Use the dependency-neutral Graph contract so that the server does not load
Roslyn. Validate graph identity, locations, edge membership and continuity of
arguments and returns on the server. Use existing node navigation in Studio.

## Option B — Analyze on interactive requests

~~~mermaid
flowchart LR
    Studio["Method inspector"] --> API["HTTP request"]
    API --> Worker["MSBuild evaluation"]
    Worker --> Report["Source witnesses"]
~~~

On-demand evaluation adds execution privileges, workspace access and
isolation dependencies to a read-only UI. Those capabilities require separate
security design.

## Proposed decision and acceptance

Try option A. Validate exact revision and selected-method membership at the
UI boundary. Keep the endpoint loopback-only, GET-only and uncached. Cover
missing and invalid evidence, direction, and real browser navigation. The
stored file is local source metadata. Runtime evidence and policy enforcement
remain pending. Revert the change to remove the optional read surface.
