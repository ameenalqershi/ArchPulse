#!/usr/bin/env bash
set -euo pipefail
# A read-only import of a real result artifact from the preceding .NET test job.
# This script does not execute arbitrary test commands or source repositories.
TRX="$(find tests -type f -path '*/TestResults/archpulse-tests.trx' -print -quit)"
if [ -z "$TRX" ]; then
  echo "No actual VSTest TRX found after dotnet test." >&2
  exit 1
fi
dotnet run --project tooling/ArchPulse.TestJourney -c Release --no-build -- --trx "$TRX" > /tmp/archpulse-trx-evidence.json
grep -q '"schema": "archpulse.test-journey.results-only/v1"' /tmp/archpulse-trx-evidence.json
grep -q '"traceStatus": "UnobservableFromTrx"' /tmp/archpulse-trx-evidence.json
grep -q '"methodCallsObserved": false' /tmp/archpulse-trx-evidence.json
echo "TRX results-only importer smoke passed; observed method calls NOT claimed."
