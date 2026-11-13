# FeatureAtlas structural candidate impact — T025/S05 partial

**2026-11-13.** This is an implemented, tested, **local-only and read-only**
source Graph change projection. [Notion T025](https://app.notion.com/p/4143830ba34d813b9f70f736bdcecd07)
is still **Planned** and S05 **Proposed**, pending human technical acceptance.
This is NOT a full Git pull-request risk or runtime-impact classifier.

## What the analyzer can and cannot prove

Inputs are **two existing, immutable GraphSnapshot revisions** for the same
project and a **human-authored candidate FeatureCatalog from the FROM revision**
(T023). Feature member mapping remains `Designed` or `Inferred` business
intent, never verified membership or accepted business contracts.

The analyzer compares the identities of source graph nodes and relation edges.
It examines added/removed source declarations and added/removed
**Static/Verified directed Calls** edges, then finds **direct candidate member**
changes or a bounded, directional static call path from a candidate feature
member to a changed symbol. Every impact has its original change-site
`SourceEvidence` plus the exact per-edge witness (including locations),
the kind of structural delta, and whether the member is directly involved
or only a static caller.

- `archpulse.feature-impact/v1`; the report includes exact FROM/TO revisions,
  FROM catalog revision, `RuntimeStatus: Unknown` and
  `CandidateImpact;StructureOnly;NotGitPRDiff;NotObservedRuntimeOrApproval`.
- An unobserved static path shows a **possible** impact, not that the path was
  executed, that a feature was affected in production, or that tests passed.
- An empty report is **NOT** proof that code or business behavior was unchanged.
  Edits to a method's body, arguments to unchanged calls, method implementation
  details, frontend code, SQL migrations and some reordered call sites are not
  represented by the current graph difference.
- Source revision keys are caller-supplied (not signed or bound to a clean Git
  worktree). The FeatureAtlas catalog is a human proposal, not authentication.
  There is no stored approval, diff patch reader, GitHub PR webhook, tenant
  authorization, or runtime OTEL trace.
- Does not follow `DependsOn`, `Inferred`, `Designed`, or observed-only
  edges as source call witnesses. Reverse graph traversal is bounded at 8
  hops, max 500 structural events, 40,000 nodes, 100,000 edges, and 10,000
  candidate impact records; HTTP/MCP outputs are further limited to 300.

## Reproducible local demonstration

In a **trusted** Linux checkout with .NET 10 SDK, run:

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/feature-signals/FeatureSignals.sln --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release
bash scripts/ci-feature-impact.sh
```

The deterministic script makes a private disposable copy of the
invoices/inventory fixture, indexes `structural-before`, imports a **manually
proposed** `reserve-inventory` candidate membership at that revision, edits
the disposable `ReserveInventoryHandler` to call a newly introduced method,
then indexes `structural-after` and checks both a direct call-edge change
and a one-step static caller path to the new method. It also confirms that
reversing FROM/TO without the original human catalog fails, rather than
generating invented feature membership.

For existing trusted, separately indexed source revisions and a previously
imported FROM catalog:

```bash
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-impact FeatureSignals --from structural-before \
  --to structural-after --store /tmp/your-trusted-graph-store
```

Read-only, **loopback-only** service interfaces when
`ARCHPULSE_GRAPH_STORE` points to the same exact local directory:

- `GET /api/graphs/{project}/feature-impact?from=...&to=...`:
  404 if graph revisions are absent; `NotIndexed` if the FROM candidate
  catalog is missing; 422 for failed validation; 413 if the bounded response
  would be exceeded. No HTTP writes.
- Ninth MCP read tool `archpulse_feature_impact(project, fromRevision,
  toRevision)`; never executes MSBuild, tests, agents or requests to GitHub.

The servers have **no public authentication**, intentionally bind only to
`127.0.0.1`, and must not be published or tunneled to other users. Source
locations and intended feature names may be sensitive.

## Outstanding work before complete T025 acceptance

Real Git commit diff integration, body/AST edit detection, changed-file-to-
symbol mapping, GitHub PR identity binding, Guardian rule finding linkage,
test selection/verification, use-case contracts, security risk scoring,
cross-repository propagation, provenance attestation, reviewer approval and
full negative-evidence modeling remain to be implemented. No full S05
acceptance or production readiness is claimed.
