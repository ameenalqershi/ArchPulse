# ArchPulse Visual Graph Studio — experimental React client

This is **not** the complete ArchPulse Web Studio of Notion S09/S27/S28/S30.
It is a real, local-only frontend for **source-derived** GraphSnapshot objects
created by the native .NET CLI and served via the ASP.NET Core Graph Read API.

## Local setup

The app requires Node 24 and a local trusted .NET development checkout.

1. Build the .NET solution and restore the Billing golden fixture.
   Commands: dotnet restore ArchPulse.sln --locked-mode;
   dotnet restore examples/billing-multi-project/Billing.sln --locked-mode;
   dotnet build ArchPulse.sln -c Release --no-restore.
2. Generate a snapshot with the C# CLI:
   dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build --
   index-solution examples/billing-multi-project/Billing.sln
   --revision golden-fixture --store /tmp/archpulse-graph
3. Start the **loopback-only** API in a dedicated terminal:
   ARCHPULSE_GRAPH_STORE=/tmp/archpulse-graph
   dotnet run --project server/ArchPulse.Server -c Release --no-build
4. Start frontend in another terminal from apps/web-studio:
   npm ci, then npm run dev.
5. Browse http://127.0.0.1:5174, enter project Billing, revision
   golden-fixture, click Open graph.

The golden fixture is only a test project, never the user's source graph.
The Vite server uses a same-origin /api proxy to 127.0.0.1:5217.
Neither server may be made publicly reachable without authentication,
authorization, HTTPS and a revised threat model.

## Implemented

- Real graph fetch by exact project/revision (no fake graph fallback).
- Canonical hierarchical scope (solution → project → namespace → type → method).
- Breadcrumb and drill-down, pointer pan, zoom, selection using full Symbol ID.
- Inspector displaying exact source revision, source location and evidence kind.
- Search and focus on exact returned symbol ID.
- Render only in-scope evidenced relationships; cross-scope related edges are
  in the inspector, not fabricated as local edges.
- Deterministic Mermaid export of visible nodes, true edges, and exact
  symbol ID mapping. This is **not** a measured runtime sequence diagram.
- Responsive local UI, keyboard select, and explicit missing-data/diagnostic states.
- S31 partial: on exact Method selection, a Tests Inspector section
  reads immutable Roslyn-static links if indexed; distinguishes NotIndexed,
  Unmapped and MappedDirectStatic without adding TestCase graph nodes or
  inventing runtime test passes, coverage, or call traces.

## Not yet implemented

Full S09/S27 S28 capabilities, virtualization for large graphs, multi-selection,
Back/Forward browser history, runtime overlays, Test Journey UI, Mermaid
roundtrip import, source editing, agent task orchestration, secure human decision
protocol, actual ChatGPT embedded MCP Apps, and public/remote deployment.

## Validation

Run npm ci, npm run build, npm run test, npm audit --audit-level=high
and check the new GitHub CI web-studio-build-test job. This client and
passing unit tests do not constitute product acceptance.

## Optional real-browser verification

The e2e/studio.spec.ts suite exercises the actual local Graph API and
React Studio in a Chromium browser, including exact source-backed
selection, hierarchy drill-down, source inspector, zoom, search,
nonexistent-revision handling, and narrow/mobile viewport behavior.

The local author test uses a trusted Billing fixture, the API on
127.0.0.1:5217 and Vite on 127.0.0.1:5174. Run the CLI graph index
step first, start both services (in separate terminal sessions),
then run npm run test:e2e from apps/web-studio after installing a
supported Chromium browser. The optional ARCHPULSE_CHROMIUM_PATH
environment variable points to an existing browser executable.

For the original author test on 2026-11-12, three real Chromium E2E
tests passed. Browser screenshots from this run were kept only in
the private authorized VPS evidence directory, not published in Git.
The automated hosted CI covers TypeScript/React build and pure graph
contract tests; it does not yet claim hosted full-browser E2E.
