#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

scratch="$(mktemp -d)"
trap 'rm -rf -- "$scratch"' EXIT
root=examples/datajourney-cross-project
cli=apps/cli/ArchPulse.Cli/bin/Release/net10.0/ArchPulse.Cli.dll
revision='demo<&>'
review() {
  dotnet "$cli" datajourney-review "$root/Journey.slnx" --revision "$revision" \
    --policy "$1" --format "$2"
}

review "$root/review-policy.json" summary > "$scratch/summary.json"
jq -e '
  .Schema == "archpulse.datajourney-review-summary/v1" and
  .Evidence == "Static" and .RuntimeStatus == "Unknown" and .Truncated and
  .Findings == 11 and
  .PolicyOutcomes == {AllowedByPolicy: 1, RequiresReview: 8, BlockedByPolicy: 2} and
  .Boundaries == {AnnotatedSink: 3, OpaqueCall: 2, UnsupportedSyntax: 4,
    DeclaredSanitizer: 1, AnalysisLimit: 1}
' "$scratch/summary.json" >/dev/null

# A return moves from callee to caller. An opposite-direction grant alone
# must not authorize it. A granted annotated-sink path becomes reviewable
# again when an input project zone is missing.
jq '.grants += [{from:"Service",to:"Entry",classification:"Confidential"}]' \
  "$root/review-policy.json" > "$scratch/reverse.json"
review "$scratch/reverse.json" summary > "$scratch/reverse-summary.json"
jq -e '.PolicyOutcomes == {AllowedByPolicy: 2, RequiresReview: 8, BlockedByPolicy: 1}' \
  "$scratch/reverse-summary.json" >/dev/null
jq 'del(.projectZones.Storage) | .grants |= map(select(.to != "Storage"))' \
  "$scratch/reverse.json" > "$scratch/missing-zone.json"
review "$scratch/missing-zone.json" summary > "$scratch/missing-summary.json"
jq -e '.PolicyOutcomes.AllowedByPolicy == 0' "$scratch/missing-summary.json" >/dev/null

# HTML metadata is encoded and summary has a fixed set of aggregate fields.
review "$root/review-policy.json" html > "$scratch/report.html"
grep -Fq 'demo&lt;&amp;&gt;' "$scratch/report.html"
grep -Fq 'Static possible.' "$scratch/report.html"
grep -Fq 'Runtime evidence: <strong>Unknown</strong>' "$scratch/report.html"
grep -Fq 'DeclaredSanitizer · RequiresReview' "$scratch/report.html"
grep -Fq 'Analysis truncated.' "$scratch/report.html"
grep -Fq '<strong>Return</strong>' "$scratch/report.html"
test "$(grep -c '<details><summary>' "$scratch/report.html")" = 11
jq -e 'keys == ["Boundaries","Evidence","Findings","PolicyOutcomes","RuntimeStatus","Schema","Truncated"]' \
  "$scratch/summary.json" >/dev/null

cat > "$scratch/invalid.json" <<'JSON'
{"sourceMethods":{},"sourceMethods":{},"projectZones":{},"grants":[]}
JSON
if review "$scratch/invalid.json" summary > "$scratch/invalid-out" 2> "$scratch/invalid-err"; then
  echo "Policy with duplicate keys was accepted." >&2
  exit 1
fi
test ! -s "$scratch/invalid-out"
grep -Fq 'duplicate properties' "$scratch/invalid-err"

jq '.unknownField = "VALUE-DO-NOT-ECHO"' "$root/review-policy.json" > "$scratch/invalid.json"
if review "$scratch/invalid.json" summary > "$scratch/invalid-out" 2> "$scratch/invalid-err"; then
  echo "Policy with unrecognized fields was accepted." >&2
  exit 1
fi
test ! -s "$scratch/invalid-out"
if grep -Eq 'FIXTURE-(SECRET|PUBLIC)-DO-NOT-SERIALIZE|classification-label|public-literal|SELECT|VALUE-DO-NOT-ECHO' \
    "$scratch/report.html" "$scratch/"*summary.json "$scratch/invalid-err"; then
  echo "Fixture values leaked into report output." >&2
  exit 1
fi
echo "T029–T031 partial: directional grants, review gaps, local path viewer and aggregate-only summary passed."
