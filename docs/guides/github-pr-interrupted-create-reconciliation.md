# T058 — Interrupted PR creation: read-only recovery (experimental)

`PortablePullRequestRecoveryCoordinator.ReconcileAsync` queries the bounded GitHub PR snapshot
reader from the T058 stack and applies the deterministic PR classifier.
It never posts a PR, changes one, or permits an automatic retry.

After a hypothetical failure between GitHub accepting `POST /pulls` and the caller
receiving its response, the reconciler reads the remote PR list through a
caller-configured HTTP client:
- Matching exact repository, head/base branch and head SHA: Confirmed *observation*, no retry
- No PR found or HTTP 4xx/5xx: Unknown, no retry
- Invalid GitHub JSON: Unknown, no retry
- Matching branch with unexpected SHA: Diverged, no retry
- Cancellation requested by the caller: propagate cancellation without mutation

The isolated tests use a deterministic fake HttpMessageHandler, recording that
**zero** write requests are made for all scenarios. This is a
network-client failure *simulation*, not proof of a real PR mutation,
authenticated GitHub identity, or exactly-once distributed semantics.
The GitHub client credentials and scopes, rate limiting and origin checking
require a dedicated trusted gateway with human-approved policy (T047).
Neither an inferred local idempotency key nor a matching PR is proof of
origin authenticity or approval. T058 remains In Progress.
