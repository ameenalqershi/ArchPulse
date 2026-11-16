# T011: MCP direct incident graph edge pagination

Stacked on #96 / #94 / #91. Adds read-only archpulse_graph_neighbors_page,
using the existing exact-revision GraphQueries.NeighborsPage v1 contract.
The tool limits size to 1..100, requires an incident cursor from that revision,
retains source evidence, and produces safe errors on invalid size/symbol/cursor.

Tests verify exact revision and edge, rejected cursor/size and missing revision.
This is no ViewScope authorization, no bound on graph snapshot loads, and no
runtime trace. Review and hosted CI remain mandatory.
