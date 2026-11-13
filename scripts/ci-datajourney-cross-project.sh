#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
set -euo pipefail

scratch="$(mktemp -d)"
trap 'rm -rf -- "$scratch"' EXIT
fixture="examples/datajourney-cross-project/Journey.slnx"
cli="apps/cli/ArchPulse.Cli"
revision="fixture-cross-project"

dotnet run --project "$cli" -c Release --no-build --   datajourney-cross-project "$fixture" --revision "$revision" > "$scratch/report.json"
dotnet run --project "$cli" -c Release --no-build --   datajourney-cross-project "$fixture" --revision "$revision" > "$scratch/repeat.json"
cmp "$scratch/report.json" "$scratch/repeat.json"

jq -e --arg rev "$revision" '
  def case($method): [.Findings[] |
    select(.SourceMethodSymbolId | test("[.]" + $method + "([(]|$)"))];
  def metadata: .Revision == $rev and .Kind == "Static" and
    .Line > 0 and .Column > 0 and (.RelativeFile | test("^(Entry|Service|Storage)/"));
  .Schema == "archpulse.datajourney-cross-project/v1" and .Revision == $rev and
  .Evidence == "Static" and .RuntimeStatus == "Unknown" and .Truncated and
  ([.Findings[] | select(.Boundary == "AnnotatedSink")] | length) == 3 and
  (case("Named") | length == 1 and .[0].Boundary == "AnnotatedSink" and
    [.[0].Hops[].Kind] == ["Argument", "Argument"] and
    (.[0].Hops[0].Call.ToSymbolId | startswith("Service::")) and
    (.[0].Hops[1].Call.ToSymbolId | startswith("Storage::"))) and
  (case("ReturnRoundTrip") | length == 1 and .[0].Boundary == "AnnotatedSink" and
    [.[0].Hops[].Kind] == ["Argument", "Return", "Argument"]) and
  (case("Read") | length == 1 and .[0].Boundary == "AnnotatedSink" and
    [.[0].Hops[].Kind] == ["Return", "Argument"]) and
  (case("Serialize") | map(.Boundary) == ["OpaqueCall"]) and
  (case("AsyncBoundary") | map(.Boundary) == ["OpaqueCall"]) and
  (case("Conditional") | map(.Boundary) == ["UnsupportedSyntax"]) and
  (case("AssignmentInArgument") | map(.Boundary) == ["UnsupportedSyntax"]) and
  (case("Recursive") | map(.Boundary) == ["AnalysisLimit"]) and
  (case("ClaimedSanitizer") | map(.Boundary) == ["DeclaredSanitizer"]) and
  (case("Overwrite") + case("Unmarked") + case("WrongArgument") | length == 0) and
  all(.Findings[]; (.Source | metadata) and (.Destination | metadata) and
    all(.Hops[]; .Call.Relation == "Calls" and (.Call.Evidence | metadata)))
' "$scratch/report.json" >/dev/null

dotnet run --project "$cli" -c Release --no-build --   index-solution "$fixture" --revision "$revision" > "$scratch/graph.json"
jq -e --slurpfile graph "$scratch/graph.json" '
  all(.Findings[].Hops[]; .Call as $call | any($graph[0].Edges[]; . == $call))
' "$scratch/report.json" >/dev/null

if grep -Eq 'FIXTURE-(SECRET|PUBLIC)-DO-NOT-SERIALIZE|classification-label|public-literal' "$scratch/report.json"; then
  echo "Fixture payload leaked into DataJourney metadata." >&2
  exit 1
fi
echo "T028 partial: three static multi-project witnesses, six explicit analysis gaps and three negative controls."
