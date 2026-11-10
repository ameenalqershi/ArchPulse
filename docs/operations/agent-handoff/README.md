# Development agent handoff (S25)

Every agent must start from [AGENTS](../../../AGENTS.md), inspect `git status`, `git log -1`, latest PRs and CI, fetch the current Notion Txxx/Sxx and restore the latest trusted checkpoint. An AI conversation is **not** a durable source of truth.

Use [handoff.template.yaml](handoff.template.yaml), [handoff.schema.json](handoff.schema.json), and a new timestamped per-task checkpoint. Do not store tokens/credentials. Record external side effects with an idempotency key, exact source and outcome; uncertain outcomes are `Unknown` and require reconciliation before retry. A write to GitHub is not Verified until the resulting remote SHA or URL is fetched independently.

S11 (runtime product continuity) is separate from S25 (agents developing ArchPulse). Do not confuse product roadmap capability with this repository operations protocol.

A replacement agent should test restoration of one small task using the checkpoint and record what evidence was missing before taking any external action.

## Machine-checkable repository handoffs

Run `python3 scripts/validate_handoffs.py` from the repository root before publishing any new `*.json` checkpoint here. This Python-standard-library-only validator checks the v1 schema's required fields and simple types, registered Txxx-to-Sxx association, full Git commit SHA shapes, canonical repository address, relative paths, status/receipt consistency, and a few recognizable secret/key signatures. It is also run by Foundation CI, with positive and negative tests in `scripts/test_handoff_validation.py`.

**Limits:** Passing means only that the public checkpoint's structure is consistent. It does **not** prove the SHA exists on GitHub, a referenced PR/CI run succeeded, an external side effect occurred, any human approved an action, or that all credentials were detected. The next agent must independently reconcile GitHub, Notion, local worktrees, and effects before resuming. Older checkpoints are immutable historical snapshots, not live status indicators. Do not add fabricated proof to satisfy a validator.

This v1 format currently permits human-readable strings for effects and tests; a future, independently reviewed schema version should use typed, hashed evidence receipts for stronger machine verification rather than silently changing old records.
