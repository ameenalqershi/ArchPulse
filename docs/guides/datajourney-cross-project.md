# Static DataJourney across C# projects

The experimental T028 command follows straight-line string values from
explicit `SensitiveSource` parameters and source methods through directly
bound static calls in a trusted C# solution. It follows arguments by Roslyn
parameter ordinal, including named arguments, and tracks identity conversion
through local assignments and returns. A finding at an explicit
`SensitiveSink` includes at least one project boundary.

Each hop contains the corresponding static graph call edge and exact source
location. `Argument` moves from caller to callee. `Return` moves from callee
back to the caller using the same call-site edge. The report includes the
source/destination locations, declared revision, and a boundary classification.

| Boundary | Meaning |
| --- | --- |
| `AnnotatedSink` | The supported static slice carries the value to a declared sink. |
| `OpaqueCall` | A call involving a tracked value cannot be followed under the analysis contract. |
| `UnsupportedSyntax` | The supported slice ends at a construct or conversion the analyzer cannot model. |
| `DeclaredSanitizer` | A declared sanitizer requires review. |
| `AnalysisLimit` | Recursion or an analysis budget interrupted the witness. |

The traversal is bounded by nesting, witness length, statement count, evaluation
steps, and finding count. `Truncated` marks an analysis limit in the report.
Branch/loop coverage, virtual dispatch, async state machines, heap and
by-reference propagation, real EF Core/SQL lineage, broker hops, and serializer
semantics remain outside this slice. An empty result carries no privacy verdict.
`RuntimeStatus` is always `Unknown`. The revision is supplied by the caller
and should be obtained from the exact trusted checkout.

The report contains identifiers and source-relative locations. It captures
no values or payloads and is printed only to the local CLI. Use trusted
solutions because MSBuild evaluates project files. Treat report files as
source metadata and keep them within the corresponding access boundary.

## Reproduce

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet restore examples/datajourney-cross-project/Journey.slnx --locked-mode
dotnet build ArchPulse.sln --no-restore -c Release
bash scripts/ci-datajourney-cross-project.sh
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  datajourney-cross-project examples/datajourney-cross-project/Journey.slnx \
  --revision "$(git rev-parse HEAD)"
```

See [T028](https://app.notion.com/p/4143830ba34d81c1ad70f6c6421f012d),
[S06](https://app.notion.com/p/4143830ba34d8141940af68ed36538be)
and the [proposed design](../decisions/ADR-0012-datajourney-cross-project-PROPOSED.md).
