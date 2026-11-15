# T011/S02 — Direct engine static-path budget limit (2026-11-16)

- Base main: c494a249b7ea0f84282e11f24b8000ba84c0b965.
- Isolated clone and branch fix/t011-path-traversal-hard-budget-20261116.
- Four xUnit parameterized cases cover 0, -1, 10,001, and int.MaxValue. Before fix the 10,001/int.MaxValue cases failed while 0/-1 already passed.
- Correction: enforce hard cap [1,10000] for ShortestStaticPath maxVisited; existing HTTP/MCP explicit cap of 5000 unchanged.
- This is only bounded BFS vertex exploration, NOT proof of bounded snapshot loading, edges index, allocation or total runtime. T011/S02 not fully accepted.
- Human review and hosted CI of exact published SHA required; no autonomous merge.
