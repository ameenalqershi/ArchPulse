# T010/S02 — Historical Graph journal replay slice (2026-11-14)

- Base: merged #67 main `8f1cec35f83a7d0199635df273a1fbe6035fa71c`.
- Scope: expose bounded-to-revision prefix validation and historical materialized
  GraphSnapshot replay via trusted CLI `graph-journal-show --revision`.
- Invariants: no mutation on read; exact project/revision evidence; no fallback
  to the head for missing revisions; corrupt/missing event prefix fails closed.
  Corruption AFTER selected revision does not prevent recovery of an earlier
  valid prefix, but `Replay()` remains the full-head verification and append
  guard. The caller MUST NOT interpret an older validated prefix as proof
  that subsequent event history is healthy.
- Security: local-only; no HTTP/MCP mutation or external listener; no
  authentication or power-loss durability claim.
- Tests: existing journal suite plus new historical test cases for exact
  revision evidence, unknown version, corruption of necessary prefix,
  and recovery from corrupt newer tail.
- Evidence: exact published SHA, local full test count, hosted CI and merge
  status must be recorded after observation; never invent a result.
- Remaining: unbounded O(history) replay cost, checkpoints/compaction,
  production integrity authority, S02 human review, T010 acceptance.
