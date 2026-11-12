# ArchPulse MCP Apps — tested local experimental integration

## Functional scope and security boundary

This is a read-only C#/.NET MCP server using the official Model Context
Protocol C# SDK. It exposes a real MCP Apps HTML UI resource
and source-evidenced tools over Streamable HTTP at
http://127.0.0.1:5231/mcp (loopback only).

This does NOT configure ChatGPT or any other remote MCP client for
authenticated internet access; OAuth/public authentication is absent.
Do not expose this port, forward it via tunnel, or treat it as a
production endpoint. No Python service, paid AI API, credentials,
or external graph database are used in this prototype.

The MCP Apps SDK is still experimental. Its diagnostic MCPEXP003 is
opted into at the specific SDK interface; NuGet vulnerability warnings
and compiler warnings-as-errors remain enabled.

## Evidence-backed architecture

~~~mermaid
flowchart TD
    A["Trusted .NET Solution"] --> B["Roslyn / MSBuildWorkspace CLI"]
    B --> C["Immutable local GraphSnapshot store"]
    C --> D["ArchPulse.McpServer (.NET 10)"]
    D --> E["MCP Tools structured JSON"]
    D --> F["MCP resource ui://archpulse/graph"]
    E --> G["MCP Apps embedded UI"]
    F --> G
    G -->|"tools/call for exact revision"|E
~~~

Each returned graph relation has exact Symbol IDs, evidence kind,
source revision and location. Static source calls are NOT measured
runtime traces. Missing revisions fail rather than producing fake data.

## Tools and UI resource

- archpulse_open_graph: exact graph plus associated MCP Apps UI.
- archpulse_graph_snapshot: structured nodes, edges and evidence.
- archpulse_symbol_context: exact selected symbol and relationships.
- archpulse_find_evidenced_path: static/verified directed path with
  deterministic traversal limit; never a runtime trace claim.
- archpulse_guardian_check: read-only proposed forbidden relation check,
  not a rule approval or policy write.
- Resource: ui://archpulse/graph, using the official MCP Apps HTML MIME.

The HTML/JS resource listens for tool input/tool result notifications,
exchanges requests with an MCP Apps host, shows hierarchical Graph nodes,
source inspector, selection, search, pan, zoom, and copies a Mermaid
representation of visible source-backed links. No CDN or direct network
requests are required from inside the iframe.

## Local trusted reproduction

~~~bash
dotnet restore examples/billing-multi-project/Billing.sln --locked-mode
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release

dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  index-solution examples/billing-multi-project/Billing.sln \
  --revision golden-mcp --store /tmp/archpulse-mcp-graph

# In a dedicated local-only terminal:
ARCHPULSE_GRAPH_STORE=/tmp/archpulse-mcp-graph \
  dotnet run --project server/ArchPulse.McpServer -c Release --no-build

# In another terminal, test using the official .NET MCP client:
dotnet run --project tooling/ArchPulse.McpSmoke -c Release --no-build -- \
  http://127.0.0.1:5231/mcp Billing golden-mcp
~~~

The CI script scripts/ci-mcp-protocol.sh creates and cleans a temporary
store, launches the loopback server, then exercises the actual client.
It runs after the trusted golden solution restore and Release build.

## Verified vs outstanding

Verified locally on 2026-11-12:
- .NET 10 Release build without errors/warnings and six MCP tool xUnit tests.
- Real C# MCP client to Streamable HTTP: 5 tools, MCP UI resource,
  11 nodes and 3 source-backed edges from the Billing golden solution,
  and missing-revision rejection.
- Three Chromium browser tests in a simulated sandboxed MCP Apps host:
  source-derived graph, navigation/selection/search, missing graph,
  and narrow/mobile viewport. The test input can be a real CLI-generated
  GraphSnapshot in ARCHPULSE_MCP_GRAPH_JSON.

Not verified:
- ChatGPT's real MCP Apps host loading this local resource. The test
  simulated a compatible host, not ChatGPT's production UI.
- Remote authenticated OAuth/TLS, per-user workspace permissions,
  safe public hosting, multi-repo indexing, agent commands, test
  execution, AI-driven engineering changes, source editing,
  runtime instrumentation, true interactive ChatGPT selection handoff.
- Full acceptance of Notion S29/S30 or all integration requirements.

Security boundaries:
- No arbitrary file paths, source code execution, repository writes, or
  network AI calls are accepted from MCP tools. Store is preconfigured.
- Source paths in Graph data may be confidential. Never expose this
  unauthenticated local endpoint on a shared or public network.
- The browser host bridge is tested in a sandbox with allow-scripts but
  without allow-forms or allow-same-origin. Opaque origin is handled.
- Remote deployment requires identity, RBAC, audit, threat modeling,
  TLS, host-origin controls, data sensitivity review, MCP Apps host
  compatibility, and proper resource authorization.
