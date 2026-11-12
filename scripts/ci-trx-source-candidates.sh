#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Offline, read-only TRX to Roslyn declaration name-candidate reconciliation.
# Runs only after tests and locked .NET build on an authorized clean checkout.
# No public API, untrusted assembly loading, inferred SUT execution or tracing.
set -euo pipefail
TMP="$(mktemp -d)"
trap 'rm -rf -- "$TMP"' EXIT
REVISION="$(git rev-parse --verify HEAD)"
TRX="tests/ArchPulse.Core.Tests/TestResults/archpulse-tests.trx"
if [ ! -s "$TRX" ]; then
  echo "Expected actual VSTest TRX from the preceding dotnet test step." >&2
  exit 1
fi
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  test-links ArchPulse.sln --revision "$REVISION" --store "$TMP" \
  > "$TMP/static-source-report.json"
dotnet run --project tooling/ArchPulse.TestJourney --no-build -c Release -- \
  --trx "$TRX" --tested-sha "$REVISION" --graph-sha "$REVISION" \
  --project ArchPulse --store "$TMP" > "$TMP/candidates.json"
grep -q '"schema": "archpulse.test-journey.source-candidates/v1"' "$TMP/candidates.json"
grep -q '"graphProject": "ArchPulse"' "$TMP/candidates.json"
grep -q '"revisionAssessment": "MatchingDeclaredRevisionUnverified"' "$TMP/candidates.json"
grep -q '"revisionEvidence": "CallerProvidedUnverified"' "$TMP/candidates.json"
grep -q '"sutOutcomeEvidence": "NoTestToSutRuntimeProof"' "$TMP/candidates.json"
grep -q '"outcomeScope": "RunnerAttemptOnly"' "$TMP/candidates.json"
grep -q '"sutExecutionEvidence": "Unobserved"' "$TMP/candidates.json"
grep -q '"perTestCoverageProven": false' "$TMP/candidates.json"
grep -Eq '"uniqueNameCandidateAttempts": [1-9][0-9]*' "$TMP/candidates.json"
echo "TRX-source candidate-only smoke passed: real test case metadata, exact source candidates; no SUT execution claims."
