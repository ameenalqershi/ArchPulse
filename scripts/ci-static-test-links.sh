#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Requires the authorized, already-restored source tree. No network indexing,
# test execution or MCP-triggered project evaluation occurs in this script.
set -euo pipefail
TEMP="$(mktemp -d)"
trap 'rm -rf -- "$TEMP"' EXIT
REVISION="ci-static-links-$(git rev-parse --verify HEAD)"
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  test-links ArchPulse.sln --revision "$REVISION" --store "$TEMP" > "$TEMP/report.json"
grep -q '"Schema": "archpulse.method-test-associations/v1"' "$TEMP/report.json"
grep -q '"GraphRevision": "'"$REVISION"'"' "$TEMP/report.json"
grep -q '"MappingScope": "RoslynStaticDirectOnly"' "$TEMP/report.json"
grep -q '"RunnerDiscovery": "NotRunnerDiscovered"' "$TEMP/report.json"
grep -q '"ResultEvidence": "NoRunOrCoverageReceipts"' "$TEMP/report.json"
grep -q '"Relation": "DirectInvocation"' "$TEMP/report.json"
grep -q '"ExecutionEvidence": "Unobserved"' "$TEMP/report.json"
grep -q '"LastResult": "Unknown"' "$TEMP/report.json"
test "$(find "$TEMP" -maxdepth 1 -name '*.graph.json' | wc -l)" -eq 1
test "$(find "$TEMP" -maxdepth 1 -name '*.test-links.json' | wc -l)" -eq 1
echo "Native Roslyn source/test sidecar smoke passed; no runtime coverage claimed."
