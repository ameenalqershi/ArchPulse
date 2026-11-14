# Local DataJourney — T027/S06 partial (2026-11-13)

**Status:** experimental **source-only** value propagation, not production privacy
protection. [Notion T027](https://app.notion.com/p/4143830ba34d8196a273f6dccadea86a)
and [S06](https://app.notion.com/p/4143830ba34d8141940af68ed36538be)
remain **Planned/Proposed**, pending human review and further work.

## What this adds

An authorized, trusted-project Roslyn MSBuild compilation analyzes **straight-line,
method-local** C# statements, without running the application. It recognizes
only explicit, fully-qualified and semantically bound annotations declared
by source in `ArchPulse.DataJourney`:

- `[SensitiveSource]` on a method parameter or a method returning a value;
- `[SensitiveSink]` on a method receiving an argument;
- `[DeclaredSanitizer]` on a method *claiming* to sanitize a value.

These are **local analysis annotations**, not a claim about actual source
confidentiality or correct sanitization. The demonstration fixture deliberately
labels a sanitizer that returns its input unchanged to prevent false safety
assurances.

For supported literal, parameter, local assignment, reassignment and
invocation statements, the engine emits a bounded, revision-exact list of
source/sink/return **symbol names**, location and outcome. **Source-code
literals, request bodies and parameter values are never retained.**

| Outcome | Precise meaning |
|---|---|
| `StaticPossibleRisk` | A directly attributable annotated source reaches an annotated sink along a supported straight-line path; possible risk, **not observed traffic** |
| `DeclaredSanitizerNeedsReview` | A method *declares* sanitization; **not evidence of safety** |
| `NoCandidateInSubset` | The inspected sink input has no candidate source in the **supported straight-line slice**; **not a privacy-safe verdict** |
| `Unknown` | An opaque method, unsupported expression, unmarked input, branch/loop/exception, or uncertainty prevents proof |
| `StaticReturnPropagation` | An annotated source is assigned and returned; return is **not automatically an external sink** |

The report has `RuntimeStatus: "Unknown"`, exact graph project/revision,
`EvidenceKind.Static`, deterministic source-line witnesses, and immutable
project/revision sidecars. It never upgrades `Observed` or `Verified`
evidence. HTTP/MCP expose only **read-only** local reports; they cannot
request MSBuild evaluation or data capture.

For calls across C# projects, see the experimental [T028 cross-project CLI](datajourney-cross-project.md).

## Reproduce trusted fixture

The following evaluates the included local .NET project: MSBuild can execute
custom targets in other solutions, so **never point these commands at untrusted
uploads or repositories without sandboxing**.

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/datajourney-local/DataJourney.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
bash scripts/ci-datajourney-local.sh
```

Or index the exact corpus without persistence:

```bash
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  datajourney-local examples/datajourney-local/DataJourney.sln \
  --revision your-exact-git-revision
```

With `--store /trusted/store`, both the canonical GraphSnapshot and
DataJourney sidecar are written once per revision. Afterwards:

```bash
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  datajourney-show DataJourney --revision your-exact-git-revision \
  --store /trusted/store
```

Local read-only Graph API:
`GET /api/graphs/DataJourney/{revision}/datajourney-local`.
Eleventh MCP tool:
`archpulse_datajourney_local(project="DataJourney", revision="...")`.
Both return `NotIndexed` when an exact source analysis has not run, **not**
a conclusion that the system carries no sensitive information. Neither tool
returns source values or generates an executable trace. Services are bound
to loopback only and have **no external authentication**; do not publish the
ports to the internet.

## Corpus, limits and measurement

The nine labeled fixture cases cover direct parameter-to-sink, declared
source-method-to-sink, false sanitizer claim, literal source replacement,
local reassignment, opaque transformation, conditional reassignment,
return propagation, and a same-short-name annotation decoy.
Expected distribution is **2 StaticPossibleRisk, 2 NoCandidateInSubset,
1 DeclaredSanitizerNeedsReview, 3 Unknown, 1 StaticReturnPropagation**.
Across **the two negative controls** the test's heuristic false-positive
count is **0/2**, which is not a statistically meaningful measurement on
real code or a claim about production precision.

A bounded 128 top-level statements per method are analyzed. Additional
method constructs can be skipped or reported unknown. There is no
interprocedural/async propagation, reflection analysis, EF Core SQL lineage,
message-bus tracking, runtime OpenTelemetry correlation, trust-boundary
detection, signatures, sanitizer verification, control-flow graph or visual
DataJourney lens yet. Unknown constructs are not classified as safe.

**Security note:** this is source-code metadata with paths and identifier
names; paths themselves can be sensitive in some deployments. Use trusted
workspaces and apply permission checks before exposing across tenants.
The sidecar is immutable for a given project/revision, but this is not a
signed security attestation; local filesystem administrators can replace it.
Do not use a static candidate report alone to make security compliance claims.

## Mixed-argument sink classification — T027/S06 bounded correction (2026-11-15)

When a declared sink has several arguments, a locally proven raw candidate
is no longer hidden merely because another argument is an unrelated unknown
local/parameter read. The result remains StaticPossibleRisk, with the raw
argument's exact static source identity. A declared sanitizer on a different
argument does not outrank this raw witness. Unknown inputs without any raw
witness remain Unknown, and unmodeled calls or unsupported prior branches
also remain Unknown because side effects can invalidate a local reading.
This is a possible static dataflow finding, not proof of a data leak,
runtime observation, a verified sanitizer or full C# control-flow analysis.
No literal parameter values are captured.
