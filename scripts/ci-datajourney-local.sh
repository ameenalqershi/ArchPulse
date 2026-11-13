#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# T027 source-only: trusted local MSBuild fixture, no sensitive payload storage.
set -euo pipefail
test -f ArchPulse.sln
TEMP="$(mktemp -d)"
trap 'rm -rf -- "$TEMP"' EXIT
mkdir -p "$TEMP/store"
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  datajourney-local examples/datajourney-local/DataJourney.sln \
  --revision ci-datajourney --store "$TEMP/store" > "$TEMP/report.json"

jq -e '
  .Schema == "archpulse.datajourney-local/v1" and
  .Project == "DataJourney" and .Revision == "ci-datajourney" and
  .RuntimeStatus == "Unknown" and
  (.Scope | contains("NoValuesStored")) and
  (.Findings | length) == 9 and
  ([.Findings[] | select(.Outcome=="StaticPossibleRisk")] | length)==2 and
  ([.Findings[] | select(.Outcome=="NoCandidateInSubset")] | length)==2 and
  ([.Findings[] | select(.Outcome=="DeclaredSanitizerNeedsReview")] | length)==1 and
  ([.Findings[] | select(.Outcome=="Unknown")] | length)==3 and
  ([.Findings[] | select(.Outcome=="StaticReturnPropagation")] | length)==1 and
  (all(.Findings[]; .Location.Kind=="Static" and
    .Location.Revision=="ci-datajourney" and .Location.Line > 0)) and
  ([.Findings[] | select(.MethodSymbolId | contains(".UnsafeDirect(")) |
    select(.Outcome=="StaticPossibleRisk")] | length)==1 and
  ([.Findings[] | select(.MethodSymbolId | contains(".BranchUncertain(")) |
    select(.Outcome=="Unknown")] | length)==1 and
  ([.Findings[] | select(.MethodSymbolId | contains(".AnnotationDecoy(")) |
    select(.Outcome=="Unknown")] | length)==1
' "$TEMP/report.json" >/dev/null

if grep -q 'DO-NOT-STORE-PAYLOAD\|non-sensitive-placeholder' "$TEMP/report.json"; then
  echo "Fixture source literal leaked into DataJourney metadata." >&2; exit 1
fi
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  datajourney-show DataJourney --revision ci-datajourney \
  --store "$TEMP/store" > "$TEMP/from-store.json"
cmp "$TEMP/report.json" "$TEMP/from-store.json"

if dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  datajourney-show DataJourney --revision ci-datajourney-missing \
  --store "$TEMP/store" >/dev/null 2>&1; then
  echo "Missing DataJourney revision incorrectly accepted." >&2; exit 1
fi

# Existing immutable sidecar can never silently be overwritten by a different
# source/report at the same logical graph revision.
sidecar="$(find "$TEMP/store" -name '*.datajourney.json' -print -quit)"
test -n "$sidecar"
sed -i 's/StaticPossibleRisk/FabricatedPrivacyProof/' "$sidecar"
if dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  datajourney-show DataJourney --revision ci-datajourney \
  --store "$TEMP/store" >/dev/null 2>&1; then
  echo "Corrupted DataJourney sidecar accepted." >&2; exit 1
fi
echo "T027 partial PASS: 2 static possible risks, 2 no-candidate-in-subset, 1 sanitizer claim, 3 unknown, 1 return propagation; fixture false positives 0/2 negative controls (not a general precision guarantee); no captured values."
