# ArchPulse roadmap — full scope preserved

Source of truth: [S20 Production Roadmap](https://app.notion.com/p/4143830ba34d81a9aebffe342e14ae12). The gates order implementation and do **not** shrink the full product to an MVP.

| Gate | Intended evidence | Status |
| --- | --- | --- |
| A — Architecture and originality | S00/S01/S02, requirements map, evidence contracts, reproducible AET research | Planned |
| B — Production-capable core | graph, Roslyn, persistence, Guardian contracts, continuity invariants | Planned |
| C — Complete capability integration | Studio, runtime, FeatureAtlas, DataJourney, RepoLab, Futures, MCP, clients, test journeys | Planned |
| D — Production hardening | sandbox, privacy, fault recovery, security, accessibility, performance benchmarks | Planned |
| E — Open-source launch readiness | license, governance, onboarding, CI, documentation, signed/verifiable release | Partially staged, NOT verified |

This package stages governance, docs and one tiny evidence-receipt sample; **it does not satisfy any complete gate**, and it does not create the public GitHub repository.

## Critical first work

- **T001 / S00:** freeze requirements registry without dropping scope; no prerequisites.
- **T002 / S01:** adopt bounded contexts after T001.
- **T053 / S11:** product handoff contract after T001 and T002.
- **T005 / S01:** full modular solution after T002.
- **T117 / S25:** developer agent handoff after T005, T053, and T105.

See [task source](https://app.notion.com/p/5f2c39784dc24174a8989488350dd0fa) for all 205 tasks and [S26](https://app.notion.com/p/4143830ba34d8185a0edc83f974c55cd) for the 192-requirement baseline. Issue creation and live progress synchronization remain pending until repository creation.