# ADR-0013 — Local DataJourney policy and evidence review

- Status: Proposed
- Date: 2026-11-13
- Scope: S06, T029, T030, T031
- Review: Human architectural and privacy review pending

## Context

The cross-project analyzer emits revision-scoped source metadata. Reviewers
need to inspect flow evidence and configured trust-zone grants, while
aggregate consumers need a smaller output that omits source identifiers.

## Option A — Generate a local view and aggregate summary

~~~mermaid
flowchart TD
    Static["Static Roslyn witnesses"] --> Policy["Directional zone policy"]
    Policy --> Local["Local expandable HTML"]
    Policy --> Summary["Aggregate JSON"]
~~~

Add an opt-in CLI projection. Display Unknown runtime evidence and retain
gaps. Use text encoding on the local view and a fixed aggregate schema for
any approved external use.

## Option B — Add browser and MCP endpoints

~~~mermaid
flowchart TD
    Static["Static witnesses"] --> API["Authenticated API"]
    API --> Studio["Web Studio view"]
    API --> Agent["Agent view"]
~~~

This supports graph navigation but depends on authorization, runtime evidence
contracts, retention and transport review. Those dependencies remain open.

## Proposed decision and acceptance

Try option A. CI must cover directional returns, missing policy mapping,
unreviewed sanitizer candidates, unsupported reflection and SQL construction,
and omission of payloads. Existing static and runtime evidence contracts
remain distinct. The repository change is reversible by reverting the PR.
