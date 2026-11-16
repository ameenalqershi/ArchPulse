# T058 — Read-only GitHub PR snapshot reader (experimental)

`GitHubPullRequestSnapshotReader.ReadAsync(client, owner, repo, ct)`
queries **all PR states** from the canonical GitHub REST endpoint with
bounded pagination (100 entries/page, maximum eight pages). The caller
must supply an appropriately credentialed and configured `HttpClient`;
this component does not create or modify pull requests.

The reader validates the returned base repository and exact head
repository, excluding fork PRs that share a branch name. It rejects
repeated PR numbers, invalid payloads, HTTP errors, and redirects to
a different API host. A full final page at the maximum bound yields
`QueryComplete=false`, which the T058 classifier treats as Unknown.
The snapshot is passed separately to
`PortablePullRequestRecovery.Evaluate`; that classifier always returns
`SafeToRetry=false`. An absent PR does not imply safe retry.

## Security / nonclaims

The reader does not establish a caller identity, inspect GitHub token
scopes, verify provenance beyond the HTTP client's HTTPS configuration,
validate human review, or provide distributed locking. It deliberately
does not execute a PR mutation. The caller must avoid forwarding
credentials or sensitive response bodies to logs. GitHub API rate limits,
permissions and pagination changes produce failures/Unknown, never an
implicit authorization to create a PR.

No real GitHub PR creation-after-timeout test or authenticated recovery
gateway end-to-end proof exists yet. T047 authentication design and
security review remain dependencies. T058 stays In Progress.
