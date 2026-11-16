# T058/S11 — Offline external-effect reconciliation (partial)

This is an experimental, deterministic **classification-only** component.
It does not call GitHub, execute an external action, or automatically retry.
The caller supplies an independently observed remote state with the same
task ID, action, and idempotency key as a validated T053 external-effect receipt.

`PortableExternalEffectReconciliation.Evaluate(local, remote)` produces
`Confirmed`, `Unknown`, or `Diverged`. **SafeToRetry is always false**.
A missing observation, one remote NotFound lookup, inconclusive result or
unverifiable receipt digest is **Unknown**, never proof that a failed PR
creation may be retried. A different task/action/key, mismatched previously
committed success digest, or remotely found effect despite a local failure
claim is **Diverged**. A valid matching Found digest yields **Confirmed** as
a *classification claim*, not proof of authentic GitHub identity.

This is not a full recovery coordinator. It does not query GitHub,
validate signed provenance, acquire a T055 fenced lease, lock the local
T054 ledger and remote mutation together, or reconcile unknown effects
into a verified state. Real external observations and retry authorization
must come from a separately reviewed gateway, with human approval for
privileged mutations. Do not mark T058 Verified, or assert exactly-once
remote effects from this experimental component.
