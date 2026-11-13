# Local DataJourney policy review

Use the CLI to evaluate static source witnesses against an explicit policy,
then open the resulting self-contained HTML file locally. The same command
can emit a fixed-field JSON summary containing aggregate counts only.

```bash
dotnet restore examples/datajourney-cross-project/Journey.slnx --locked-mode
dotnet run --project apps/cli/ArchPulse.Cli -c Release -- \
  datajourney-review examples/datajourney-cross-project/Journey.slnx \
  --revision "$(git rev-parse HEAD)" \
  --policy examples/datajourney-cross-project/review-policy.json \
  --format html > /tmp/archpulse-datajourney-review.html
```

Use `--format summary` to omit symbol IDs, source paths, project, revision
and zone labels. Counts still describe the analyzed repository and may
require permissions before sharing with a model or another service.

## Policy input and results

The [fixture policy](../../examples/datajourney-cross-project/review-policy.json)
uses exact source method IDs, maps project names to operator-defined zones,
and grants a classification permission for a specific direction. Supported
classifications are Public, Internal, Confidential and Restricted. Use the
cross-project JSON report to copy source method IDs. Classification applies
to the source method; finer distinctions between parameters and fields
remain open.

| Outcome | Meaning |
| --- | --- |
| `AllowedByPolicy` | The annotated-sink witness has a known classification and every recorded hop satisfies the supplied zone policy. |
| `BlockedByPolicy` | At least one recorded cross-zone hop lacks a matching directional grant. |
| `RequiresReview` | Classification, zone, path completion or sanitizer evidence needs review. |

Returns move from callee to caller when checking direction. The report
preserves Unknown runtime status and surfaces truncated analysis, unsupported
syntax, opaque calls and sanitizer claims. The policy evaluates witnesses
offline. Application enforcement, field classifiers and authenticated
approvals remain follow-up work.

## Privacy corpus

The controlled fixture includes three labeled positive paths and three
labeled negative controls. The CI smoke checks that all six match their
labels, with 3/3 sensitivity and 3/3 precision within those six cases. Eight
additional cases deliberately stop with an analysis gap. The gap cases include
an unverified sanitizer, reflection and a constructed SQL string. They are
excluded from the precision and sensitivity calculation because the analyzer
does not model their behavior. The fixture performs no database or broker
operation.

The smoke checks that input string literals and SQL text stay out of HTML and
summary output. HTML escapes identifier, policy and revision text, has no
script and loads no external assets. It still contains source metadata and
belongs in the trusted local workspace. Use only trusted MSBuild solutions.

See [T029](https://app.notion.com/p/4143830ba34d812dbe2aef92b872f8a2),
[T030](https://app.notion.com/p/4143830ba34d81dca435d73848498346) and
[T031](https://app.notion.com/p/4143830ba34d81598397e780adce7ed0).
