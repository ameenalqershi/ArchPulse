#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Execute only in a trusted checkout: the indexer evaluates local MSBuild projects.
set -euo pipefail
test -f ArchPulse.sln
TEMP="$(mktemp -d)"
trap 'rm -rf -- "$TEMP"' EXIT
mkdir -p "$TEMP/store"
revision="ci-feature-catalog"
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
    index-solution examples/billing-multi-project/Billing.sln \
    --revision "$revision" --store "$TEMP/store" >"$TEMP/graph.json"
jq -e '([.Edges[] | select(.Relation == "Calls")] | length) > 0' "$TEMP/graph.json" >/dev/null
project="$(jq -r '.Project' "$TEMP/graph.json")"
source="$(jq -r 'first(.Edges[] | select(.Relation == "Calls") | .FromSymbolId)' "$TEMP/graph.json")"
target="$(jq -r 'first(.Edges[] | select(.Relation == "Calls") | .ToSymbolId)' "$TEMP/graph.json")"
jq -n --arg project "$project" --arg revision "$revision" --arg source "$source" --arg target "$target" '{
  schema:"archpulse.feature-catalog/v1",project:$project,revision:$revision,
  features:[{id:"create-invoice",title:"Create Invoice",description:"Manual CI fixture only",
    owner:"CI fixture",criticality:"P0",scenarios:["Candidate invocation"],
    acceptanceContracts:["Review before approval"], members:[
      {symbolId:$source,reason:"Selected real source call",claim:"Designed"},
      {symbolId:$target,reason:"Selected real target",claim:"Inferred"}]}]
}' >"$TEMP/manifest.json"

dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
    feature-import "$project" --revision "$revision" --store "$TEMP/store" \
    --manifest "$TEMP/manifest.json" >"$TEMP/import.json"
jq -e '.importedFeatures == 1 and .executionStatus == "Unknown"' "$TEMP/import.json" >/dev/null
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
    feature-show "$project" --revision "$revision" --store "$TEMP/store" \
    --id create-invoice >"$TEMP/scope.json"
jq -e --arg source "$source" --arg target "$target" '
    .Schema == "archpulse.feature-scope/v1" and
    .ExecutionStatus == "Unknown" and .ObservedCoverage == false and
    .MembershipScope == "ManualCandidateMembership;StaticInternalGraphEdgesOnly" and
    (.Nodes | length) == 2 and
    ([.StaticEdges[] | select(.FromSymbolId == $source and .ToSymbolId == $target)] | length) > 0
' "$TEMP/scope.json" >/dev/null
if dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
    feature-show "$project" --revision "$revision" --store "$TEMP/store" \
    --id invented-business-feature >"$TEMP/forged.out" 2>/dev/null; then
    echo "Unexpected implicit feature creation" >&2
    exit 1
fi
echo "FeatureAtlas: exact real-source CLI import/read, immutable candidate membership and negative scope checks PASS."
