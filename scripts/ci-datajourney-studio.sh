#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

scratch="$(mktemp -d)"
server=
studio=
cleanup() {
  if [ -n "$studio" ]; then kill "$studio" 2>/dev/null || true; fi
  if [ -n "$server" ]; then kill "$server" 2>/dev/null || true; fi
  rm -rf -- "$scratch"
}
trap cleanup EXIT
fixture=examples/datajourney-cross-project/Journey.slnx
revision=studio-fixture
server_port=25217
studio_port=25174
dotnet apps/cli/ArchPulse.Cli/bin/Release/net10.0/ArchPulse.Cli.dll \
  datajourney-cross-project "$fixture" --revision "$revision" --store "$scratch" > "$scratch/report.json"
ARCHPULSE_LOCAL_GRAPH_PORT="$server_port" ARCHPULSE_GRAPH_STORE="$scratch" \
  dotnet server/ArchPulse.Server/bin/Release/net10.0/ArchPulse.Server.dll \
  > "$scratch/server.log" 2>&1 &
server=$!
ARCHPULSE_LOCAL_GRAPH_PORT="$server_port" apps/web-studio/node_modules/.bin/vite apps/web-studio --host 127.0.0.1 --port "$studio_port" --strictPort \
  > "$scratch/vite.log" 2>&1 &
studio=$!

for url in "http://127.0.0.1:$server_port/health" "http://127.0.0.1:$studio_port/"; do
  ready=
  for attempt in $(seq 1 30); do
    if ! kill -0 "$server" "$studio" 2>/dev/null; then break; fi
    if curl -fsS "$url" > /dev/null 2>&1; then ready=1; break; fi
    sleep 1
  done
  if [ -z "$ready" ]; then
    cat "$scratch/server.log" "$scratch/vite.log" >&2
    exit 1
  fi
done
(
  cd apps/web-studio
  ARCHPULSE_STUDIO_BASE_URL="http://127.0.0.1:$studio_port" ARCHPULSE_DATAJOURNEY_REVISION="$revision" npm run test:e2e -- e2e/datajourney.spec.ts
)
