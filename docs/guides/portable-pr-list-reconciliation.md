# T058 — PR listing reconciliation, offline follow-up

This implementation adds `PortablePullRequestRecovery.Evaluate` to classify a
**caller-supplied** GitHub pull-request listing against a prior
`github.create-pr` effect. It does not contact GitHub itself.

The caller must independently authenticate and inspect the relevant
repository, pull requests in **all relevant states**, and pagination. The
input includes `QueryComplete`, candidate number/repository/head/base/commit,
the expected request identity, and the existing T053 external-effect receipt.

- No list, truncated list, or no matching PR: **Unknown**; no retry.
- Multiple matching head/base/repository PRs or duplicate PR numbers:
  **Diverged**; no retry.
- One matching PR with a different head SHA: **Diverged**; no retry.
- One matching PR while local claim was Failed: **Diverged**; no retry.
- Exactly one matching PR and matching expected SHA: **Confirmed**
  as an observation **only**, not a trusted authorization or proof of
  genuine GitHub provenance. Still **no retry**.

The result's `SafeToRetry` property is **always false**. This avoids the
dangerous assumption that a single NotFound result proves an interrupted
create operation did not succeed.

## Not implemented

No authenticated GitHub discovery, no pagination implementation, no external
mutation, no signatures, no synchronized T054 checkpoint+T055 lease+GitHub
transaction, no automatic retry and no actual failure-before-response
integration exercise. The caller's `QueryComplete` flag is **untrusted**;
verification of its provenance is required before product acceptance.
This is a local experimental T058 follow-up only, not Verified.

This change is stacked on PR #88 and depends transitively on the T053–T057
branches. Do not merge pending dependencies without consolidating the branches
and reviewing S11/S14 security requirements.
