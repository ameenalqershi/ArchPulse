# S25 development-agent handoff validation coverage (T117)

**Scope:** historical, public, source-controlled checkpoints used by development agents. This is **not** the runtime Cross-Agent Continuity feature (S11), a verified independent handoff (T118), or a secrets scanner guarantee.

## Implementations, provenance and gaps

| Validator | Source | Checks | Currently in required Foundation CI? |
| --- | --- | --- | --- |
| Native .NET foundation | [HandoffChecker.cs](../../../tooling/ArchPulse.RepositoryValidator/HandoffChecker.cs) | Immutable foundation audit envelope, source IDs, schema fields, SHA shapes and known status | Yes, as part of the native repository validator |
| PR #23 experimental Python validator | [validate_handoff.py](../../../scripts/validate_handoff.py) | Multiple JSON checkpoints, handoff v1 schema, safe paths, idempotency marker and common secret patterns | **No**; run separately |
| PR #25 experimental Python validator | [validate_handoffs.py](../../../scripts/validate_handoffs.py) | Required field types, task-to-spec relations, SHA and canonical repository shape, relative paths, status/receipt consistency and selected key patterns | **No**; run separately |

The two Python implementations were independently proposed on the prior foundation and have **different coverage**. A successful run of either is not proof of cross-agent restoration. The long-term target is one tested **C#/.NET** validator covering their distinct security checks, with no duplicate Python build gate.

## Reproducible offline checks

Run from the repository root (no external AI service required):

    dotnet restore ArchPulse.sln --locked-mode
    dotnet run --project tooling/ArchPulse.RepositoryValidator
    python3 scripts/validate_handoff.py
    python3 scripts/validate_handoffs.py
    python3 -m unittest discover -s scripts -p 'test_handoff_validator.py' -v
    python3 -m unittest discover -s scripts -p 'test_handoff_validation.py' -v

When a checkpoint validation fails, fix the checkpoint or validator contract with a reviewable commit. Never change immutable historical receipts to claim an external action happened. Use the [independent receiver rehearsal](REHEARSAL.md), [PR #23 sample](T117-2026-11-10.json) and [PR #25 sample](T117-PR25-2026-11-10.json) as proposed structural evidence, then **independently fetch** referenced GitHub/Notion state, CI and actual source SHA before describing recovery as successful.

## Required native migration acceptance

A follow-up C# migration must validate all committed candidate JSON files with explicit v1 structure, supported paths, canonical repository identity, task/spec relationships, bounded arrays, status/receipt consistency, and detectable credential patterns; use negative fixtures and deterministic CLI exit codes. Keep Unknown distinct from Verified. Avoid attributing an external side effect to any agent based solely on self-reported checkpoint text.

**Current status:** S25 T117 remains *partially evidenced*; T118 is not complete merely because validator tests pass. No production authentication, sandbox or cross-agent runtime verification is provided by these scripts.
