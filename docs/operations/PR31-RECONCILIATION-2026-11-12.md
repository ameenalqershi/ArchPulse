# PR #31 — alternative rebuild reconciliation

**Date:** 2026-11-12. **Disposition:** retain the original PR #31 Git parent and its dated engineering evidence while selecting the newer, separately verified source implementations already in main. This is a merge-resolution record, **not** a claim that every line from the older branch was incorporated or that the whole product is complete.

## Source and branch history

- Original alternative: [PR #31](https://github.com/ameenalqershi/ArchPulse/pull/31), head 5369413199c9c246fc5cc2656720de395333098f, baseline main 6602183884bfb7b6dcd24803fb1d5db1ccdc28db.
- Main at reconciliation start: 29c904d58451123dc39deea370fed4ba981ab440 after reviewed foundation, governance, security and license-audit merges.
- Preserve the original [status report](REBUILD-STATUS-2026-11-12.md) and [handoff record](agent-handoff/historical/REBUILD-2026-11-12.json) as **historical self-reported observations**. The old checkpoint is archived with unchanged bytes under a separate historical directory because its 2026-era changedFiles entries include directory names, incompatible with the later S25 exact-file-path contract; do not silently alter those original claims to force validator success. They are not live acceptance records, independently verified external effects or an instruction to repeat stale operations.

## Chosen source implementations

| Earlier alternative in PR #31 | Selected current implementation in main | Evidence status |
| --- | --- | --- |
| Roslyn source graph, node IDs, static calls | [PR #32](https://github.com/ameenalqershi/ArchPulse/pull/32), MSBuildWorkspace solution graph and cross-project calls | Static source evidence, incomplete unknown coverage |
| Basic foundation validators and CLI | [PR #30](https://github.com/ameenalqershi/ArchPulse/pull/30) plus [PR #32](https://github.com/ameenalqershi/ArchPulse/pull/32) | Tested C# implementation |
| Guardian forbidden-dependency evaluator | [PR #32](https://github.com/ameenalqershi/ArchPulse/pull/32) | Preliminary policies, not authenticated approval |
| ASP.NET Core loopback read API | [PR #33](https://github.com/ameenalqershi/ArchPulse/pull/33) | Local-only, unauthenticated prototype |
| Interactive React graph explorer | [PR #34](https://github.com/ameenalqershi/ArchPulse/pull/34) | Tested browser prototype, not whole design system |
| AI/ChatGPT interactive architecture surface | [PR #35](https://github.com/ameenalqershi/ArchPulse/pull/35) | Native MCP Apps host simulator, not verified in real ChatGPT |
| Agent handoffs and registry constraints | [PR #23](https://github.com/ameenalqershi/ArchPulse/pull/23), [PR #25](https://github.com/ameenalqershi/ArchPulse/pull/25), [PR #26](https://github.com/ameenalqershi/ArchPulse/pull/26), [PR #28](https://github.com/ameenalqershi/ArchPulse/pull/28) | Proposed contracts, not completed product requirements |

## Conflict resolution

PR #31 and the later build both added the same solution, Roslyn, Graph, Guardian, CLI, API, Studio and CI paths with incompatible versions. Do **not** copy duplicate Graph models, old Python-based CI, parallel tool projects or stale lockfiles into the active main tree. The reconciliation merge gives precedence to current main for these conflicting paths while retaining original git ancestry and the two explicitly dated evidence documents. No original PR #31 source code is silently represented as adopted when its API is not in current main.

## Historic S25 checkpoint compatibility

PR #25 recorded three external-effects objects with a self-reported state of "verified" but no idempotency key; PR #23 later introduced a stricter *prospective* protocol requiring scoped idempotency keys and states such as succeeded or unknown. To preserve immutable source evidence, the older checker tolerates **only** the exact SHA-256-pinned PR25 file and **only** its known idempotency/state errors. It continues to enforce envelope and secret checks, and new or changed checkpoint objects are rejected as before. This grandfathered structural compatibility is **not** a verified external effect, authenticated actor, release approval or formal schema upgrade.

## Remaining work

This reconciliation does not certify semantic equivalence of all alternative indexing algorithms. Proposed capabilities requiring real engineering: test-to-method links and execution traces (S31/S33), FeatureAtlas/DataJourney, incremental indexing, Futures, authenticated Guardian waivers, sandboxing, runtime OTEL, ChatGPT-hosted MCP Apps verification and production authorization. Any distinct PR #31 behavior should be reintroduced by a small, source-backed test and reviewed C# PR rather than parallel duplicate modules.

**Acceptance note:** validate repository links and checkpoint contracts on the candidate tree, verify current GitHub CI for the resulting commit and leave the old report immutable. Passing these checks is not acceptance of all S00–S33 specifications.
