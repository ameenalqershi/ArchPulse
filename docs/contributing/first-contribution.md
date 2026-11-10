# First contribution walkthrough

1. Read [CONTRIBUTING](../../CONTRIBUTING.md), this [developer guide](../guides/developer-guide.md) and [governance](../../GOVERNANCE.md).
2. Find a documented `good first issue` or propose a documentation/fixture improvement in Discussions; ordinary contributors do not need repository membership.
3. Comment on the issue to avoid duplicating work. Fork, then create `docs/Txxx-short-topic` or `fix/Txxx-short-topic`.
4. Implement a small, independently verifiable change and update relevant Sxx references.
5. Run `dotnet restore`, `dotnet build`, `dotnet test` and `python3 scripts/validate_foundation.py` where applicable.
6. Open a Pull Request using the template. Link the issue, real command output, tests and security consideration.
7. Follow reviewer feedback and wait for successful CI and human review. A PR is not a verified Notion task by itself.

`good first issue` must have clear acceptance criteria, no privileged credentials and a limited blast radius.
