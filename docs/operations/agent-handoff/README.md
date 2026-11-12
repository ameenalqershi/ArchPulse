# Development agent handoff (S25)

Every agent must start from [AGENTS](../../../AGENTS.md), inspect `git status`, `git log -1`, latest PRs and CI, fetch the current Notion Txxx/Sxx and restore the latest trusted checkpoint. An AI conversation is **not** a durable source of truth.

Use [handoff.template.yaml](handoff.template.yaml), [handoff.schema.json](handoff.schema.json), and a new timestamped per-task checkpoint. Do not store tokens/credentials. Record external side effects with an idempotency key, exact source and outcome; uncertain outcomes are `Unknown` and require reconciliation before retry. A write to GitHub is not Verified until the resulting remote SHA or URL is fetched independently.

S11 (runtime product continuity) is separate from S25 (agents developing ArchPulse). Do not confuse product roadmap capability with this repository operations protocol.

A replacement agent should test restoration of one small task using the checkpoint and record what evidence was missing before taking any external action.

## CI-checked checkpoint contract and independent receiver protocol

Run `python3 scripts/validate_handoff.py` to check all committed checkpoint JSON files against the published v1 envelope, safe Git SHAs, idempotency-key discipline and known credential signatures. Run `python3 -m unittest discover -s scripts -p "test_*.py"` for adversarial tests. These are structural checks, **not independent attestation** of external results. See [the receiver rehearsal procedure](REHEARSAL.md) and [T117 checkpoint](T117-2026-11-10.json). Do not claim T118 verified until a newly started second agent reproduces the evidence without conversation history.
