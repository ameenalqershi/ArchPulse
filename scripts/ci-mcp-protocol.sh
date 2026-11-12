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
  http://127.0.0.1:5231/mcp Billing ci-mcp-fixture
echo "MCP protocol PASS: real C# client, tools, resources and exact revision."
