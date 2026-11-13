#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Runs only on a TRUSTED fixture. MSBuild evaluation may run project code.
set -euo pipefail
test -f ArchPulse.sln
TEMP="$(mktemp -d)"
trap 'rm -rf -- "$TEMP"' EXIT
mkdir -p "$TEMP/source" "$TEMP/store"
for file in FeatureSignals.sln FeatureSignals.csproj InvoiceController.cs \
            ReserveInventoryHandler.cs InvoiceScenarioTests.cs packages.lock.json; do
  cp "examples/feature-signals/$file" "$TEMP/source/$file"
done
dotnet restore "$TEMP/source/FeatureSignals.sln" --locked-mode >/dev/null

# Capture the first revision from real Roslyn declarations; no fabricated
# symbol IDs, AI suggestions, runtime call traces, or test success claims.
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-discover "$TEMP/source/FeatureSignals.sln" --revision structural-before \
  --store "$TEMP/store" >"$TEMP/before-signals.json"
member="$(jq -er 'first(.Signals[] | select(.Kind == "NamedHandler" and
          .SuggestedFeatureId == "reserve-inventory") | .SymbolId)' "$TEMP/before-signals.json")"
jq -n --arg member "$member" '{
  schema:"archpulse.feature-catalog/v1",project:"FeatureSignals",revision:"structural-before",
  features:[{id:"reserve-inventory",title:"Reserve Inventory",
    description:"Manual candidate feature for a structural diff smoke",
    owner:"CI fixture",criticality:"P0",
    scenarios:["Candidate inventory reservation"],acceptanceContracts:["Human review required"],
    members:[{symbolId:$member,reason:"Candidate entry only",claim:"Designed"}]}]
}' >"$TEMP/candidate.json"
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-import FeatureSignals --revision structural-before \
  --store "$TEMP/store" --manifest "$TEMP/candidate.json" >"$TEMP/import.json"

# Modify only the disposable fixture: previously named handler delegates to
# a newly introduced, source-backed method. Does not alter the user checkout.
sed -i \
  -e 's/=> !string.IsNullOrWhiteSpace(sku);/=> CommitReservation(sku);/' \
  -e '/public bool Handle(string sku)/a\    public bool CommitReservation(string sku) => !string.IsNullOrWhiteSpace(sku);' \
  "$TEMP/source/ReserveInventoryHandler.cs"
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-discover "$TEMP/source/FeatureSignals.sln" --revision structural-after \
  --store "$TEMP/store" >"$TEMP/after-signals.json"

dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-impact FeatureSignals --from structural-before --to structural-after \
  --store "$TEMP/store" >"$TEMP/impact.json"
jq -e '
  .Schema == "archpulse.feature-impact/v1" and
  .CatalogRevision == "structural-before" and
  .EvidenceScope == "CandidateImpact;StructureOnly;NotGitPRDiff;NotObservedRuntimeOrApproval" and
  .RuntimeStatus == "Unknown" and .StructuralChangesExamined >= 2 and
  any(.Impacts[]; .FeatureId == "reserve-inventory" and
       .ChangeKind == "NodeAdded" and .Basis == "StaticCallerPath" and
       (.StaticCallWitness | length) == 1 and
       .StaticCallWitness[0].Evidence.Kind == "Static") and
  any(.Impacts[]; .FeatureId == "reserve-inventory" and
       .ChangeKind == "CallAdded" and .Basis == "DirectCandidateMember")
' "$TEMP/impact.json" >/dev/null

if dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
  feature-impact FeatureSignals --from structural-after --to structural-before \
  --store "$TEMP/store" >"$TEMP/negative.json" 2>/dev/null; then
  echo "Missing FROM candidate catalog was not rejected" >&2
  exit 1
fi
echo "T025 partial PASS: source-backed structural change, direct and caller-path candidate impact, unknown direction rejected."
