# Reviewer onboarding

Reviewers are trusted for expertise, not automatically granted repository write/admin privileges. See [GOVERNANCE](../../GOVERNANCE.md), [CODE_OF_CONDUCT](../../CODE_OF_CONDUCT.md) and [CONTRIBUTING](../../CONTRIBUTING.md).

## Before your first review

1. Read the linked Sxx specification and Txxx issue, dependencies, architectural boundaries and proposed acceptance criteria.
2. Confirm source revision and tests. Do not accept an agent-produced success claim without a reproducible receipt.
3. Flag security-sensitive changes, external network/process execution, secret handling, sandbox boundaries and untrusted inputs for specialist review.
4. Request readable diagrams (valid Mermaid per S32) for architectural alternatives and identify who authorized a decision.
5. Check compatibility and consumer contracts. Ask for negative/failure-mode tests and updates to docs where needed.
6. Keep reviews constructive; distinguish blocking correctness, security and scope concerns from non-blocking style preferences.
7. Review CI check names and results for the **exact** PR head SHA. An older green workflow is not current proof.
8. Do not merge your own PR without an independently authorized exception. The single-owner repository may lack a second reviewer; document this instead of fabricating approval.

## Evidence review

Use `Verified` only with a matching commit/build/test/trace receipt. Treat interruptions, missing traces, canceled checks or uncertain external effects as `Unknown` until reconciled. Any release-critical security check or waived architecture rule must have a documented review and expiration.

## Becoming a reviewer

Demonstrate consistent high-quality contributions and respectful, accurate feedback. A maintainer may nominate an individual; responsibilities and permissions are effective only after explicit acceptance and owner-authorized grant. Reviewer status is not required to open an issue or PR.
