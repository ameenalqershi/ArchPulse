# Governance and technical decision process

ArchPulse is an owner-led project at foundation stage. Formal GitHub roles are granted only by an authorized account owner after review. A GitHub account with a PR is a contributor, not automatically a member. The documented roles below describe intended responsibilities, not current assignments.

| Role | Typical responsibilities | Authority |
| --- | --- | --- |
| Contributor | issues, documentation, code and test PRs | no repository write access required |
| Regular Contributor | recurring quality contributions, mentoring | no implied elevated access |
| Reviewer | technical review in assigned areas | review comments; optional triage/read rights after authorization |
| Module Maintainer | module ownership, releases of agreed components | scoped `write`/`maintain` if necessary and approved |
| Core Maintainer | cross-module review, security coordination, release gating | restricted `maintain`/`write` subject to owner policy |
| Project Owner | final license, access, budget, protected architecture decision | actual verified repository owner |

## Admission, promotion, suspension

Nomination is a public issue or Discussion containing prior contributions, area, time commitment, conflicts of interest, and two review endorsements **when reviewers exist**. The owner verifies identity, permissions and consent before any invite. A probationary period and periodic access reviews are recommended. Inactivity or misconduct can trigger revocation after notice where safe, with documented reasons and appeal to an independent maintainer when available. No automatic elevation after N PRs.

## Decisions and changes

- Routine scoped PRs: a qualified reviewer approves where available; single-maintainer bootstrap exceptions are disclosed and logged.
- Cross-module/irreversible changes: Architecture Decision Package (ADP), current-vs-proposed Mermaid per option, risks, evidence, specs, and **human approval recorded** before protected changes.
- Security incidents: private handling per SECURITY.md, with responsible disclosure.
- License/ownership/trademark/CLA policy: explicit owner decision and public record, never silently changed by an agent.
- Consensus is preferred; unresolved disagreements are escalated to the owner with rationale and an appeal path after the team expands.

## Maintainer succession

If no maintainer remains active, the project becomes maintenance-limited; transfer of ownership needs the legal owner's explicit authorization. No automated agent may transfer a repository or promote itself to admin.