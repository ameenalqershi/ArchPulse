#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Trusted-local operator fixture only, using explicit fake reviewer identity.
set -euo pipefail
test -f ArchPulse.sln
TEMP="$(mktemp -d)"
trap 'rm -rf -- "$TEMP"' EXIT
mkdir -p "$TEMP/graph"

dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  index-solution examples/billing-multi-project/Billing.sln \
  --revision ci-human-review --store "$TEMP/graph" > "$TEMP/index.json"

source="$(jq -er 'first(.Nodes[] | select(.Kind == "Method") | .SymbolId)' "$TEMP/index.json")"
jq -n --arg source "$source" '{
  schema:"archpulse.feature-catalog/v1", project:"Billing", revision:"ci-human-review",
  features:[{id:"create-invoice",title:"Create Invoice",
    description:"Human review test fixture only",owner:"CI fixture",criticality:"P0",
    scenarios:["Proposed invoice flow"],acceptanceContracts:["Manual review pending"],
    members:[{symbolId:$source,reason:"Manually proposed candidate",claim:"Designed"}]}]
}' > "$TEMP/catalog.json"
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-import Billing --revision ci-human-review --store "$TEMP/graph" \
  --manifest "$TEMP/catalog.json" > /dev/null

dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-review-show Billing --revision ci-human-review --store "$TEMP/graph" > "$TEMP/zero.json"
jq -e '.Sequence == 0 and .Head == "GENESIS" and
  all(.Members[]; .Disposition == "Unreviewed") and .Scope ==
  "LocalHumanDeclared;NotAuthenticated;NotBusinessVerified"' "$TEMP/zero.json" >/dev/null

jq -n --arg source "$source" '{
  action:"ApproveCandidate",actorKind:"LocalHumanDeclared",actor:"CI human label (unverified)",
  reason:"Explicit scripted test fixture, not actual human certification",
  expectedHead:"GENESIS",featureId:"create-invoice",symbolId:$source
}' > "$TEMP/approve.json"
# Deliberate explicit confirmation applies ONLY to the synthetic fixture.
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-review-record Billing --revision ci-human-review --store "$TEMP/graph" \
  --request "$TEMP/approve.json" --confirm-local-human-review > "$TEMP/approved.json"
head="$(jq -er '.Head' "$TEMP/approved.json")"
jq -e '.Sequence == 1 and .History[0].ActorKind == "LocalHumanDeclared" and
  .Members[0].Disposition == "OperatorApprovedCandidate" and
  .History[0].PreviousHash == "GENESIS"' "$TEMP/approved.json" >/dev/null

# Automated actor, even with exact current head, has no permission to override.
jq -n --arg source "$source" --arg head "$head" '{
  action:"RejectCandidate",actorKind:"Automation",actor:"CI bot",
  reason:"Try to overwrite operator decision",expectedHead:$head,
  featureId:"create-invoice",symbolId:$source
}' > "$TEMP/automation.json"
if dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-review-record Billing --revision ci-human-review --store "$TEMP/graph" \
  --request "$TEMP/automation.json" --confirm-local-human-review >/dev/null 2>&1; then
  echo "Automated overwrite accepted" >&2; exit 1
fi

# Real local-human-declared rollback to genesis, recorded as a NEW immutable receipt.
jq -n --arg head "$head" '{
  action:"Rollback",actorKind:"LocalHumanDeclared",
  actor:"CI human label (unverified)",
  reason:"Synthetic rollback restores sequence zero",expectedHead:$head,
  rollbackToSequence:0
}' > "$TEMP/rollback.json"
dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-review-record Billing --revision ci-human-review --store "$TEMP/graph" \
  --request "$TEMP/rollback.json" --confirm-local-human-review > "$TEMP/rolled.json"
jq -e '.Sequence == 2 and (.History | length) == 2 and
  .History[1].Action == "Rollback" and .History[1].RollbackToSequence == 0 and
  .History[1].PreviousHash == .History[0].Hash and
  all(.Members[]; .Disposition == "Unreviewed")' "$TEMP/rolled.json" >/dev/null

# Tamper with one receipt and prove that local CLI fails closed.
receipt="$(find "$TEMP/graph" -name '000001.review-receipt.json' -print -quit)"
test -n "$receipt"
sed -i 's/CI human label (unverified)/Forged reviewer identity/' "$receipt"
if dotnet run --project apps/cli/ArchPulse.Cli --no-build -c Release -- \
  feature-review-show Billing --revision ci-human-review --store "$TEMP/graph" >/dev/null 2>&1; then
  echo "Tampered local reviewer receipt was trusted" >&2; exit 1
fi
echo "T026 partial PASS: local operator review, automated overwrite rejected, immutable rollback receipts and hash-chain tampering detected."
