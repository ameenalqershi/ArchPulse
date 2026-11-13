#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Run only on a trusted checkout: MSBuild project evaluation occurs in CLI.
set -euo pipefail
ROOT="$(pwd)"
TEMP="$(mktemp -d)"
MCP_PID=''
cleanup() {
  if [ -n "$MCP_PID" ]; then kill "$MCP_PID" 2>/dev/null || true; fi
  rm -rf -- "$TEMP"
}
trap cleanup EXIT
mkdir -p "$TEMP/graph"
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  index-solution examples/billing-multi-project/Billing.sln \
  --revision ci-mcp-fixture --store "$TEMP/graph" >"$TEMP/index.json"
# Exact second graph revision for T025 comparison. It intentionally has no
# structural changes; an empty result must not claim that a Git PR is safe.
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  index-solution examples/billing-multi-project/Billing.sln \
  --revision ci-mcp-fixture-future --store "$TEMP/graph" >"$TEMP/index-next.json"

# Use a real, statically resolved call in the trusted fixture. Business membership
# is a manually authored candidate, not a source-proven business claim.
jq -e '([.Edges[] | select(.Relation == "Calls")] | length) > 0' "$TEMP/index.json" >/dev/null
source="$(jq -r 'first(.Edges[] | select(.Relation == "Calls") | .FromSymbolId)' "$TEMP/index.json")"
target="$(jq -r 'first(.Edges[] | select(.Relation == "Calls") | .ToSymbolId)' "$TEMP/index.json")"
jq -n --arg source "$source" --arg target "$target" '{
  schema:"archpulse.feature-catalog/v1",project:"Billing",revision:"ci-mcp-fixture",
  features:[{id:"create-invoice",title:"Create Invoice",description:"Manual protocol fixture",
    owner:"MCP CI fixture",criticality:"P0",scenarios:["Explicit scenario"],
    acceptanceContracts:["Human review pending"], members:[
      {symbolId:$source,reason:"Candidate source",claim:"Designed"},
      {symbolId:$target,reason:"Candidate target",claim:"Inferred"}]}]
}' >"$TEMP/manifest.json"
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-import Billing --revision ci-mcp-fixture --store "$TEMP/graph" \
  --manifest "$TEMP/manifest.json" >"$TEMP/import.json"

# T026: deliberately synthetic local reviewer receipt; no authenticated
# human identity or verified feature behavior is claimed.
jq -n --arg source "$source" '{
  action:"ApproveCandidate", actorKind:"LocalHumanDeclared",
  actor:"MCP CI synthetic reviewer label", reason:"Fixture only; not actual human acceptance",
  expectedHead:"GENESIS", featureId:"create-invoice", symbolId:$source
}' >"$TEMP/review.json"
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-review-record Billing --revision ci-mcp-fixture \
  --store "$TEMP/graph" --request "$TEMP/review.json" \
  --confirm-local-human-review >"$TEMP/review-summary.json"
jq -e '.Sequence == 1' "$TEMP/review-summary.json" >/dev/null

# A separate real ASP.NET/Handler/Test source fixture uses the same
# pre-configured local store but is never converted to approved features.
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-discover examples/feature-signals/FeatureSignals.sln \
  --revision ci-mcp-fixture --store "$TEMP/graph" >"$TEMP/signals.json"
jq -e '(.Signals | length) == 4' "$TEMP/signals.json" >/dev/null

# T027: real trusted C# source corpus, exact revision, metadata only.
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  datajourney-local examples/datajourney-local/DataJourney.sln \
  --revision ci-mcp-fixture --store "$TEMP/graph" > "$TEMP/datajourney.json"
jq -e '(.Findings | length) == 9' "$TEMP/datajourney.json" >/dev/null

ARCHPULSE_GRAPH_STORE="$TEMP/graph" \
dotnet server/ArchPulse.McpServer/bin/Release/net10.0/ArchPulse.McpServer.dll \
  >"$TEMP/mcp-server.log" 2>&1 &
MCP_PID="$!"

HEALTHY=0
for i in $(seq 1 25); do
  if curl --fail --silent --max-time 2 http://127.0.0.1:5231/health > /dev/null; then
    HEALTHY=1
    break
  fi
  if ! kill -0 "$MCP_PID" 2>/dev/null; then
    cat "$TEMP/mcp-server.log" >&2
    exit 1
  fi
  sleep 1
done
if [ "$HEALTHY" != 1 ]; then
  cat "$TEMP/mcp-server.log" >&2
  echo "MCP endpoint was not healthy in the bounded local startup window." >&2
  exit 1
fi
dotnet run --project tooling/ArchPulse.McpSmoke --no-build -c Release -- \
  http://127.0.0.1:5231/mcp Billing ci-mcp-fixture FeatureSignals
echo "MCP protocol PASS: real C# client, tools, resources and exact revision."
