# T011 — Bounded legacy neighbor results (2026-11-16)

- Main base: a3a1dcec62140f8a1a0074bd1241312fba6ffd7d.
- The full-list GraphQueries.Neighbors previously allowed unlimited incident-edge results and allocation. A 10,000 edge cap now fails closed with an instruction to use NeighborsPage.
- A red regression was first reproduced on the authorized VPS against 10,001 incident edges (Assert.Throws failed because no exception was thrown). The final branch is recreated using the GitHub connector because the VPS Desktop Commander monthly tool allowance was exhausted.
- The regression also expects the existing NeighborsPage to retrieve 25 edges from the same high-degree snapshot.
- This is NOT a cap on loading/validating GraphSnapshot, nor does it guarantee API compatibility, runtime performance, authorization, or T011 acceptance.
- Hosted CI on the published SHA and human code review are the required next gates. No autonomous merging.
