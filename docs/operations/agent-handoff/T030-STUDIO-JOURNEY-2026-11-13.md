# T030 Web Studio DataJourney handoff

- Parent: [PR #52](https://github.com/ameenalqershi/ArchPulse/pull/52), commit `46dc4245157004ac8f31dab424c38b6518de2cd5`.
- Branch: `feat/s06-studio-cross-project-datajourney-t030-20261113`.
- Status: partial T030. See [T030](https://app.notion.com/p/4143830ba34d81dca435d73848498346).

## Implemented

The CLI can save the exact graph and its immutable cross-project DataJourney
sidecar. The loopback GET endpoint returns witnesses for the selected method.
It validates source evidence, revision, graph edge membership, and hop
continuity. The Studio inspector can expand paths and navigate argument and
return destinations. Runtime evidence remains unknown. Existing report
wire names and canonical graph schema are retained.

## Verify

Run the locked .NET restore and Release build, then the full .NET test suite.
Run the Web Studio build and Vitest suite after the locked npm install.
With Chromium installed, run `bash scripts/ci-datajourney-studio.sh`. It
indexes the trusted three-project fixture and runs API and Vite on isolated
loopback ports. The browser opens the graph, expands a witness and follows
it across projects. Hosted CI runs the same browser test. Reconcile the
published revision and check results before declaring hosted verification.

## Open

The stack is intended for human review in parent-first order. Retarget
after confirming how the parents integrate. Runtime telemetry correlation,
EF Core/SQL and broker semantics, authenticated distributed Studio access,
and non-method DataJourney navigation remain open. Inspect
[ADR-0014](../../decisions/ADR-0014-datajourney-studio-PROPOSED.md)
before extending the API surface.
