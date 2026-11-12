// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchPulse.TraceabilityValidator;

/// <summary>S26 proposed requirement-ID snapshot integrity, not implementation acceptance.
/// Native replacement for the historical Python baseline/registry smoke.</summary>
public static partial class RequirementBaselineChecker
{
    private const string Source = "https://app.notion.com/p/4143830ba34d8185a0edc83f974c55cd";
    private static readonly Dictionary<string,string> Detailed = new(StringComparer.Ordinal)
    {
        ["R-NAV"]="S27",["R-VGOV"]="S28",["R-MCP-DEC"]="S29",
        ["R-BCANVAS"]="S30",["R-ITEST"]="S31",["R-MERMAID"]="S32",
        ["R-TJ"]="S33"
    };
    private static readonly HashSet<string> NotReady =
        ["tbd","todo","n/a","none","unknown","—","pending"];

    [GeneratedRegex(@"^(R-[A-Z][A-Z0-9-]*)-[0-9]{3}$",RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
    [GeneratedRegex(@"^T[0-9]{3}(?:,T[0-9]{3})*$",RegexOptions.CultureInvariant)]
    private static partial Regex TaskRefs();

    public static IReadOnlyList<string> Validate(string root)
    {
        var errors = new List<string>();
        var path=Path.Combine(root,"docs/requirements/requirement-baseline-lock.json");
        JsonElement record;
        try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));
            record=doc.RootElement.Clone();
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException)
        {
            return ["requirement-baseline-lock.json: unreadable lock: "+e.Message];
        }
        if(record.ValueKind!=JsonValueKind.Object)
            return ["requirement-baseline-lock.json: must be a JSON object"];
        if(Str(record,"schema")!="archpulse-requirement-baseline/v1")
            errors.Add("lock: unsupported schema");
        if(Str(record,"source")!=Source || Str(record,"sourceSpecification")!="S26")
            errors.Add("lock: canonical S26 provenance missing");
        if(Str(record,"designStatus")!="proposed")
            errors.Add("lock: baseline must remain proposed");
        if(!record.TryGetProperty("snapshotCount",out var count) ||
           count.ValueKind!=JsonValueKind.Number || count.GetInt32()!=192)
            errors.Add("lock: current reviewed snapshot has 192 requirements");
        if(!record.TryGetProperty("requirementIds",out var ids) ||
           ids.ValueKind!=JsonValueKind.Array ||
           ids.EnumerateArray().Any(x=>x.ValueKind!=JsonValueKind.String))
            return [..errors,"lock: requirementIds must be a string array"];
        var lockedIds=ids.EnumerateArray().Select(x=>x.GetString()??"").ToArray();
        if(lockedIds.Length!=192 || lockedIds.Distinct(StringComparer.Ordinal).Count()!=lockedIds.Length ||
            !lockedIds.SequenceEqual(lockedIds.Order(StringComparer.Ordinal)))
            errors.Add("lock: expected 192 unique, sorted requirement IDs");
        if(lockedIds.Any(x=>!IdPattern().IsMatch(x)))
            errors.Add("lock: invalid requirement ID");
        if(!record.TryGetProperty("familyPrimarySpecifications",out var mappingJson) ||
           mappingJson.ValueKind!=JsonValueKind.Object ||
           mappingJson.EnumerateObject().Any(x=>x.Value.ValueKind!=JsonValueKind.String))
            return [..errors,"lock: familyPrimarySpecifications must be a string map"];
        var mapping=mappingJson.EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.GetString()??"",StringComparer.Ordinal);
        foreach(var (family,spec) in Detailed)
            if(!mapping.TryGetValue(family,out var got) || got!=spec)
                errors.Add("lock: S27-S33 family mappings must retain their canonical specs");
        var families=lockedIds.Select(x=>IdPattern().Match(x))
            .Where(m=>m.Success).Select(m=>m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        if(!mapping.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(families))
            errors.Add("lock: each family must have one primary specification");
        var validSpecs=Enumerable.Range(0,34).Select(x=>$"S{x:00}").ToHashSet(StringComparer.Ordinal);
        foreach(var (family,spec) in mapping)
            if(!validSpecs.Contains(spec))
                errors.Add($"lock: invalid primary specification for {family}: {spec}");

        var requirements=TraceabilityChecker.ReadCsv(
            Path.Combine(root,"docs/requirements/requirements.csv"),
            ["Requirement ID","Expected behavior","Notion tasks",
             "Acceptance evidence required","Implementation status","Source specification"],errors);
        var tasks=TraceabilityChecker.ReadCsv(
            Path.Combine(root,"docs/requirements/task-registry.csv"),
            ["Work ID","Task","Area","Gate","Priority","Status in Notion",
             "Depends On IDs","Specification","Requirement IDs","Notion URL"],errors);
        var taskSpecs=tasks.ToDictionary(row=>row["Work ID"],row=>row["Specification"],StringComparer.Ordinal);
        var actual=requirements.Select(row=>row["Requirement ID"]).ToArray();
        if(actual.Length!=192 || actual.Distinct(StringComparer.Ordinal).Count()!=actual.Length)
            errors.Add("requirements.csv: expected 192 unique rows");
        if(!lockedIds.ToHashSet(StringComparer.Ordinal).SetEquals(actual))
            errors.Add("requirement ID drift: review Notion S26 before changing lock");
        foreach(var row in requirements)
        {
            var id=row["Requirement ID"];var match=IdPattern().Match(id);
            if(!match.Success){errors.Add($"{id}: invalid ID");continue;}
            var family=match.Groups[1].Value;
            if(!mapping.ContainsKey(family))errors.Add($"{id}: untracked family");
            var raw=row["Notion tasks"];
            if(!TaskRefs().IsMatch(raw))
            {errors.Add($"{id}: malformed comma-separated Txxx task references");continue;}
            var refs=raw.Split(',');
            if(refs.Distinct(StringComparer.Ordinal).Count()!=refs.Length)
                errors.Add($"{id}: duplicate task references");
            foreach(var task in refs)
                if(!taskSpecs.ContainsKey(task))errors.Add($"{id}: unknown task {task}");
            if(Detailed.TryGetValue(family,out var detailedSpec) &&
                !refs.Any(t=>taskSpecs.TryGetValue(t,out var value) && value==detailedSpec))
                errors.Add($"{id}: no linked task traces to {detailedSpec}");
            foreach(var key in new[]{"Expected behavior","Acceptance evidence required"})
                if(string.IsNullOrWhiteSpace(row[key]) ||
                   NotReady.Contains(row[key].Trim().ToLowerInvariant()))
                    errors.Add($"{id}: no actionable {key}");
            if(row["Implementation status"] is not("Planned" or "Unknown"))
                errors.Add($"{id}: unsupported implementation claim");
            if(row["Source specification"]!="S26")
                errors.Add($"{id}: source snapshot must identify S26");
        }
        return errors;
    }

    private static string? Str(JsonElement item,string name)=>
        item.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String ?
        value.GetString():null;
}
