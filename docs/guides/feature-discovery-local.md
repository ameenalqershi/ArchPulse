# T024 / S05 — Offline source-signal feature discovery (partial)

**2026-11-13 — experimental.** Notion [T024](https://app.notion.com/p/4143830ba34d81e98370d11a764da0c6)
and [S05](https://app.notion.com/p/4143830ba34d81ea8d5ce9f71abd9595)
remain **Planned/Proposed** until formally reviewed. This is a bounded, reproducible,
trusted-local Roslyn slice, NOT automatic confirmed FeatureAtlas membership.

## Why source signals are not business features

A class called `ReserveInventoryHandler` is a strong clue about business intent,
not proof that it is registered, invoked, or actually reserves inventory.
Likewise, a declared `[HttpPost]` attribute does not prove an endpoint is reachable,
and an `[Fact]` annotation does not prove test discovery, passage or coverage.

All signals therefore retain:
- `Source.Kind: Static`: exact known graph Method SymbolId and original source
  location at the caller-supplied revision.
- `BusinessClaim: Inferred`: never `Verified`, `Observed` or `Static`.
- `RuntimeStatus: Unknown` and discovery scope
  `SourceSignalsOnly;HumanReviewRequired;NotFeatureMembership`.
- No mutation of the canonical GraphSnapshot or the T023 operator-approved
  catalog; candidate names do not add graph parents or automatic membership.

The current detectors are intentionally narrow:

| Signal | Trigger (Roslyn semantic model) | What remains unverified |
| --- | --- | --- |
| `ControllerAction` | Declared method in a type named `*Controller`, with an attribute whose resolved type name is one of `Microsoft.AspNetCore.Mvc.HttpGet/Post/Put/Delete/PatchAttribute` | Controller registration, ASP.NET host, inheritance/conventions, prefix templates, filters, route conflicts, auth |
| `NamedHandler` | Declared method named `Handle` or `HandleAsync` inside a `*Handler` class | Request/command registration, DI, dispatch semantics, transactions, intent |
| `TestDeclaration` | Source method with a recognized xUnit/NUnit/MSTest attribute from existing S31 source declaration index | Runner enumeration, individual parameterized cases, result, test effectiveness, SUT coverage |

Even a recognized fully qualified type name is a compilation observation, not
authentication of a framework assembly, configured runtime host or business meaning.
HTTP `RouteFragment` contains only a literal method-level attribute parameter,
**not** a full effective URL. Dynamic/unsafe route values are skipped, not guessed.
Names may be wrong; review is mandatory. No paid AI service or Python is used.

## Reproduce offline on Linux

Only evaluate **trusted** MSBuild solutions: projects can run build logic during
workspace evaluation. Do not bind the unauthenticated servers to public networks.

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/feature-signals/FeatureSignals.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore

dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-discover examples/feature-signals/FeatureSignals.sln \
  --revision fixture-exact-source-sha --store /tmp/archpulse-signals \
  > /tmp/archpulse-discovery.json

dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-signals FeatureSignals --revision fixture-exact-source-sha \
  --store /tmp/archpulse-signals
```

The command writes immutable `*.feature-signals.json` beside the *separate*
revisioned graph snapshot, using a private local directory. Re-importing the same
bytes is idempotent; changing the report at the same project/revision fails.
Revision values are caller-provided: the CLI **does not authenticate** git HEAD,
source tree cleanliness or built DLL provenance. Store integrity validation checks
source references and claim types but is not a signed attestation. Do not feed
untrusted JSON directly to a public MCP service.

With the same `ARCHPULSE_GRAPH_STORE`:
- `GET /api/graphs/{project}/{revision}/feature-signals` returns
  `NotIndexed` for missing reports (not zero verified features).
- `archpulse_feature_signals(project, revision)` is an eighth local **read-only**
  MCP tool. It returns bounded, source-anchored, review-required suggestions.
- Both use a 500-signal response cap. No HTTP/MCP import route, arbitrary source
  path input, script execution, runtime trace or feature approval endpoint.

## Deterministic real fixtures and negative tests

The [fixture](../../examples/feature-signals/) includes:
- Invoice `InvoicesController` with **real ASP.NET Core** `HttpPost` and
  `HttpGet` metadata; two `ControllerAction` signals.
- `ReserveInventoryHandler.Handle` with no DI binding; one
  `NamedHandler` hint.
- An explicit **compile-only** `Xunit.FactAttribute` test-name fixture with
  no runner; one `TestDeclaration` signal.
- A decoy non-MVC `HttpPostAttribute` and helper class, both correctly ignored.

Run `bash scripts/ci-feature-discovery.sh` plus the real 8-tool
`bash scripts/ci-mcp-protocol.sh`; .NET xUnit tests cover deterministic
IDs, read/write exactness, revision mismatches, forged evidence, corrupted
sidecars, 404/422, absent reports and no side-effect writes.

## Gaps and next steps

**Still not supported:** MVC minimal APIs or conventional route maps,
full `RouteAttribute` resolution, MediatR interface and DI registration,
OpenAPI schemas, domain events, behavioral feature grouping, cross-repo
mapping, false-positive adjudication and feedback history (T024), full Git PR diff
or Guardian feature linking (T025; [partial structural candidate graph impact](feature-impact-local.md)), approved correction and locking (T026),
feature-scoped UI, DataJourney and observed feature Test Journeys. In particular,
this T024 report does **not** populate the T023 `BusinessFeature` catalog
automatically. Specifications remain Proposed and no product acceptance
is claimed.
