# Repository operations / maintainer checklist

Repository: [ameenalqershi/ArchPulse](https://github.com/ameenalqershi/ArchPulse). Visibility: Public. Default branch: main. Apache-2.0 in root.

- Verify CI on a commit before configuring mandatory status contexts or merge gates.
- Enable Issues, Discussions, Actions, safe forking and private vulnerability reporting where accessible.
- Create valid labels, milestones for Gate A–E, and tracking issues; never flood 205 indistinct issues.
- Protect main from force pushes/deletion, require PRs and existing passing checks. Require independent reviews only after another maintainer accepts the role, to avoid locking the initial single-owner project.
- Keep CODEOWNERS limited to actual permitted accounts, with no invented maintainers.
- Publish branch/ruleset/admin constraints as `Blocked` or `Planned`, never mark unverified setting as enforced.
