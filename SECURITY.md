# Security policy

## Scope

There is **no production ArchPulse release** yet. The repository foundation's source, workflows, documentation and dependencies are in scope for security reports. Threats involving untrusted repository builds, prompt injection, arbitrary execution, path traversal, source leaks, approvals and fake evidence will be assessed before product implementation.

## Private disclosure

After creating the public GitHub repository, use GitHub's **Private vulnerability reporting** feature if enabled. Otherwise contact the verified owner privately through GitHub rather than opening a public issue with exploit details. The owner must configure an accessible security contact before claiming a mature response SLA.

Do not publish secrets, personal data, or active exploit payloads. Do not attack third-party services or execute untrusted repositories on a maintainer's host. The project will coordinate remediation, tests and a public advisory as appropriate, with timing targets but no guaranteed deadline.

## Required controls

Least-privilege workflow tokens, no secrets for untrusted fork PRs, dependency review, commit/revision provenance, sandbox isolation, redaction by default, human-approved privileged actions, and independent evidence verification. An AI-generated recommendation is not an authorization or a verified security finding.