# Foundation verification ledger

Repository: https://github.com/ameenalqershi/ArchPulse
Environment: authorized Linux VPS, /opt/archpulse
Scope: repository foundation only; major product features not implemented.

| Evidence | Status | Basis |
|---|---|---|
| Correct public GitHub repository | Verified | GitHub connector get_repo, owner + visibility |
| Apache-2.0 license | Verified | GitHub repository shows Apache-2.0 |
| 34 Notion specs / 205 tasks / 192 requirements | Verified as design index | Notion source view and S26 parse |
| .NET SDK | Verified local | dotnet --list-sdks: 10.0.114 |
| .NET Release build | Verified local | 0 warnings, 0 errors |
| Unit tests | Verified local | 4 passed, 0 failed |
| GitHub CI | Pending | Do not report green until run completes successfully |
| Branch protection / Discussions / Projects | Pending | Must inspect live GitHub settings |
| Full product acceptance | Planned | Not covered by foundation tests |

This ledger is revised when remote evidence exists. Absence of evidence is recorded as Unknown rather than success.
