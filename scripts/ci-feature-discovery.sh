#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Trusted local golden fixture ONLY: solution indexing evaluates MSBuild project files.
set -euo pipefail
test -f ArchPulse.sln
TEMP="$(mktemp -d)"
trap 'rm -rf -- "$TEMP"' EXIT
mkdir -p "$TEMP/store"
revision="ci-source-signals"
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
    feature-discover examples/feature-signals/FeatureSignals.sln \
    --revision "$revision" --store "$TEMP/store" >"$TEMP/first.json"
dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
    feature-signals FeatureSignals --revision "$revision" \
    --store "$TEMP/store" >"$TEMP/loaded.json"

# Exact source declarations and no promotions into runtime/feature proof.
jq -e '
    .Schema == "archpulse.feature-discovery/v1" and
    .GraphProject == "FeatureSignals" and .GraphRevision == "ci-source-signals" and
    .DiscoveryScope == "SourceSignalsOnly;HumanReviewRequired;NotFeatureMembership" and
    .RuntimeStatus == "Unknown" and
    (.Signals | length) == 4 and
    ([.Signals[] | select(.Kind == "ControllerAction")] | length) == 2 and
    ([.Signals[] | select(.Kind == "NamedHandler")] | length) == 1 and
    ([.Signals[] | select(.Kind == "TestDeclaration")] | length) == 1 and
    all(.Signals[]; .BusinessClaim == "Inferred" and .Source.Kind == "Static" and
      .Source.Revision == "ci-source-signals") and
    ([.Signals[] | select(.SymbolId | contains("DeleteInvoices"))] | length) == 0 and
    ([.Signals[] | select(.SymbolId | contains("InventoryHelper"))] | length) == 0
' "$TEMP/first.json" >/dev/null
cmp "$TEMP/first.json" "$TEMP/loaded.json"

# Missing sidecar must fail, not return fabricated empty verified evidence.
if dotnet run --project apps/cli/ArchPulse.Cli -c Release --no-build -- \
    feature-signals FeatureSignals --revision nonexistent \
    --store "$TEMP/store" >"$TEMP/negative.log" 2>/dev/null; then
    echo "Missing revision was improperly accepted" >&2
    exit 1
fi
# Discovery must not silently create a reviewed operator business catalog.
if compgen -G "$TEMP/store/*.features.json" >/dev/null; then
    echo "Automatic business feature approval detected" >&2
    exit 1
fi
echo "T024 exact-source signals PASS: 2 MVC candidates, 1 handler naming hint, 1 test declaration; unknown and decoy rejected."
