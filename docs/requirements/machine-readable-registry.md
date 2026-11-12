# Machine-readable S26 requirements registry (T001)

**Status: a deterministic export of proposed/planned design data, not implemented product functionality.**

The source index is [Notion S26](https://app.notion.com/p/4143830ba34d8185a0edc83f974c55cd), with [S00](https://app.notion.com/p/4143830ba34d81e9b337d04a89706acc) as the full-product charter and [task T001](https://app.notion.com/p/4143830ba34d817797f0e03b0551a6bb) as the work item. The export uses the existing public [192-row snapshot](requirements.csv), [205-task index](task-registry.csv), [34-specification index](specification-index.md), and [reviewed ID lock](baseline-lock-guide.md) without copying private Notion pages or making network calls.

## Free/offline commands

Run from repository root with **.NET 10 SDK and Python 3**. The small Python export adapter now **invokes the native C# traceability validator** and fails closed if that dependency is unavailable; it does not restore the removed Python validator. Restore the already-pinned .NET dependencies once (no paid AI service is required):

    dotnet restore ArchPulse.sln --locked-mode
    dotnet run --project tooling/ArchPulse.TraceabilityValidator --no-restore
    python3 scripts/requirement_registry.py
    python3 scripts/requirement_registry.py --requirement R-VGOV-015
    python3 scripts/requirement_registry.py --json > /tmp/archpulse-requirements.json
    python3 -m unittest discover -s scripts -p 'test_*.py' -v

The generated payload conforms to the public [registry schema](requirements-registry.schema.json) and contains fixed provenance, linked task dependencies, candidate document ownership, expected evidence, and **empty evidence receipts**. A content SHA-256 identifies the exact *input snapshot* used; it is not a signature or test receipt.

## Distinguishing states without false completion

- **Full-product scope target:** MandatoryFullProduct means the S00 charter does not permit deleting a capability merely to make delivery easier. It **does not** mean a particular detailed design choice is approved.
- **Design approval:** Proposed refers to S26 and current specification status. The primary owner for each requirement family is a *candidate* and remains Pending review.
- **Implementation:** Planned comes from the S26 snapshot, not code inspection or a GitHub Issue.
- **Verification:** Unknown and an empty receipts list mean no executed acceptance evidence has been linked to that requirement.
- **Task dependencies:** Literal Txxx references from the Notion-derived task CSV, with cross-specification integration tasks preserved.

## Change and acceptance control

The export must fail closed when the stable ID lock or task graph is inconsistent; it must not silently repair source records. Changes to the owner-specification crosswalk, scope classification, source data, or JSON contract require a separate review against the latest Notion S00/S26, and an ADR when the contract becomes public/consumed. Do not auto-update the lock merely to appease CI. Changing counts in the source requires explicit source/acceptance review.

This v1 CLI enables tools and agents to inspect planned data. The Python exporter is a transitional offline compatibility tool; C#/.NET is the canonical platform, and native registry export remains future engineering work. **Not completed:** semantic completeness of S00–S33, formal approval of all requirement-to-owner mappings, live Notion synchronization, actual acceptance-test receipts, deployment gates or the ArchPulse product itself. Parent T001 remains open until all of those applicable acceptance conditions have real evidence.
