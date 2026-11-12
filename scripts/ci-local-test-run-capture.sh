#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Executes only the trusted native Core.Tests project in a clean CI checkout.
# Local process/digest receipt only: no sandbox, no test-to-SUT trace or coverage.
set -euo pipefail
ROOT="$(pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf -- "$WORK"' EXIT
mkdir "$WORK/results"
TARGET="tests/ArchPulse.Core.Tests/ArchPulse.Core.Tests.csproj"
if dotnet run --project tooling/ArchPulse.TestJourney -c Release --no-build -- \
  --capture "$TARGET" --root "$ROOT" --output-dir "$WORK/results" \
  >/dev/null 2>/dev/null; then
  echo "Local test runner must require explicit --allow-local-execution." >&2
  exit 1
fi
if [ -n "$(ls -A "$WORK/results")" ]; then
  echo "Unauthorized capture created output files." >&2
  exit 1
fi
dotnet run --project tooling/ArchPulse.TestJourney -c Release --no-build -- \
  --capture "$TARGET" --root "$ROOT" --output-dir "$WORK/results" \
  --allow-local-execution > "$WORK/receipt.json"
test -s "$WORK/results/archpulse-local-captured.trx"
test -s "$WORK/receipt.json"
REVISION="$(git rev-parse HEAD)"
HASH="$(sha256sum "$WORK/results/archpulse-local-captured.trx" | cut -d ' ' -f 1)"
grep -Fq '"schema": "archpulse.test-journey.local-run-receipt/v1"' "$WORK/receipt.json"
grep -Fq "\"sourceGitCommit\": \"$REVISION\"" "$WORK/receipt.json"
grep -Fq "\"trxSha256\": \"$HASH\"" "$WORK/receipt.json"
grep -Fq '"gitEvidence": "GitRevParseAndCleanStatusBeforeAfterOnLocalCheckout"' "$WORK/receipt.json"
grep -Fq '"assemblyEvidence": "ConventionalPrebuiltOutputFileHashNotRunnerBinaryAttestation"' "$WORK/receipt.json"
grep -Fq '"runnerExitCode": 0' "$WORK/receipt.json"
grep -Fq '"testOutcomeScope": "TRXOutcomeAppliesToRunnerAttemptOnly"' "$WORK/receipt.json"
grep -Fq '"sutExecutionEvidence": "NoSutCallOrReturnEvidence"' "$WORK/receipt.json"
grep -Fq '"sutMethodCallsObserved": false' "$WORK/receipt.json"
grep -Fq '"perTestCoverageProven": false' "$WORK/receipt.json"
grep -Eq '"testAttemptCount": [1-9][0-9]*' "$WORK/receipt.json"
echo "Explicit local trusted VSTest process observed; git clean, dll/TRX digests recorded. No SUT call claims."
