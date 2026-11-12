// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace ArchPulse.RepositoryValidator;

/// <summary>Offline structural and secret-hygiene review of ALL public S25 checkpoints.
/// Does not authenticate upstream services, remote operations, agent capabilities, or all secrets.
/// Historical waiver is exact-file/byte-digest-bound, never a general bypass.</summary>
public static partial class HandoffAllChecker
{
    private const string HistoricFile="T117-PR25-2026-11-10.json";
    private const string HistoricSha="2b3d9366304ff1e802c9ddbd3e83a2171e049a347acfc4bb581c8fa00fb636ac";
    private static readonly HashSet<string> Sensitive =
        ["password","secret","private_key","access_token","api_key",
         "authorization","refresh_token","client_secret"];
    private static readonly HashSet<string> ValidStatuses =
        ["planned","in-progress","blocked","review","verified","unknown"];
    private static readonly HashSet<string> EffectStates =
        ["planned","succeeded","failed","unknown"];

    [GeneratedRegex(@"^[0-9a-f]{40}$",RegexOptions.CultureInvariant)]
    private static partial Regex Sha();
    [GeneratedRegex(@"^T[0-9]{3}$",RegexOptions.CultureInvariant)]
    private static partial Regex TaskId();
    [GeneratedRegex(@"^S[0-9]{2}$",RegexOptions.CultureInvariant)]
    private static partial Regex SpecId();
    [GeneratedRegex(@"^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?$",RegexOptions.CultureInvariant)]
    private static partial Regex RepoUrl();
    [GeneratedRegex(@"(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{15,}|sk-[A-Za-z0-9_-]{25,}|AKIA[A-Z0-9]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)",RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"reconcil|inspect|verify|تحقق|افحص",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex ReconcileAction();

    public static IReadOnlyList<string> Validate(string root)
    {
        root=Path.GetFullPath(root);
        var errors=new List<string>();
        var folder=Path.Combine(root,"docs/operations/agent-handoff");
        JsonElement schema;
        Dictionary<string,string> tasks;
        try
        {
            using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"handoff.schema.json")));
            schema=json.RootElement.Clone();
            tasks=ReadTasks(Path.Combine(root,"docs/requirements/task-registry.csv"));
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException
                                or InvalidDataException or MalformedLineException)
        {
            return ["unable to read handoff schema or registry: "+e.Message];
        }
        var schemaProps=Get(schema,"properties");
        var required=Get(schema,"required");
        if(schemaProps.ValueKind!=JsonValueKind.Object || required.ValueKind!=JsonValueKind.Array)
            return ["malformed handoff schema"];
        var allowed=schemaProps.EnumerateObject().Select(p=>p.Name).ToHashSet(StringComparer.Ordinal);
        var must=required.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String)
            .Select(x=>x.GetString()??"").ToHashSet(StringComparer.Ordinal);
        if(!must.SetEquals(allowed))errors.Add("handoff schema required/properties disagree");
        var paths=Directory.GetFiles(folder,"*.json").Where(x=>Path.GetFileName(x)!="handoff.schema.json")
            .Order(StringComparer.Ordinal).ToArray();
        if(paths.Length==0)errors.Add("No committed JSON checkpoint exists");
        if(!paths.Any(x=>Path.GetFileName(x).StartsWith("FOUNDATION",StringComparison.Ordinal)))
            errors.Add("missing public foundation checkpoint");
        foreach(var path in paths)
        {
            var name=Path.GetFileName(path);
            byte[] raw;
            JsonElement record;
            try
            {
                if(new FileInfo(path).Length is < 1 or > 256*1024)
                {errors.Add(name+": checkpoint exceeds 256 KiB");continue;}
                raw=File.ReadAllBytes(path);
                using var json=JsonDocument.Parse(raw);
                record=json.RootElement.Clone();
            }
            catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException)
            {errors.Add(name+": invalid checkpoint: "+e.Message);continue;}
            var local=ValidateRecord(record,schemaProps,must,tasks);
            bool exactOld=name==HistoricFile &&
                Convert.ToHexStringLower(SHA256.HashData(raw))==HistoricSha;
            foreach(var problem in local)
                if(!exactOld || !Regex.IsMatch(problem,
                    @"^externalEffects\[[0-9]+\]: (missing idempotencyKey|non-empty idempotencyKey required|invalid state)$",
                    RegexOptions.CultureInvariant))
                    errors.Add(name+": "+problem);
        }
        return errors;
    }

    private static List<string> ValidateRecord(JsonElement r,JsonElement schemaProps,
        HashSet<string> required,Dictionary<string,string> tasks)
    {
        var errors=new List<string>();
        if(r.ValueKind!=JsonValueKind.Object)
            return ["checkpoint must be a JSON object"];
        var present=r.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
        foreach(var k in required.Except(present))errors.Add("missing required field: "+k);
        foreach(var k in present.Except(required))errors.Add("unrecognized field: "+k);
        foreach(var item in r.EnumerateObject())
        {
            if(!schemaProps.TryGetProperty(item.Name,out var rule))continue;
            var allowedTypes=Get(rule,"type");
            var types=allowedTypes.ValueKind switch
            {
                JsonValueKind.String => [allowedTypes.GetString()??""],
                JsonValueKind.Array => allowedTypes.EnumerateArray()
                    .Select(x=>x.GetString()??"").ToArray(),
                _ => Array.Empty<string>()
            };
            if(types.Length>0 && !types.Any(t=>MatchesType(item.Value,t)))
            {errors.Add(item.Name+": incorrect JSON type");continue;}
            var c=Get(rule,"const");
            if(c.ValueKind!=JsonValueKind.Undefined && c.ToString()!=item.Value.ToString())
                errors.Add(item.Name+": wrong schema version");
            var en=Get(rule,"enum");
            if(en.ValueKind==JsonValueKind.Array &&
                !en.EnumerateArray().Any(x=>x.ToString()==item.Value.ToString()))
                errors.Add(item.Name+": unknown status");
            var pattern=Get(rule,"pattern");
            if(pattern.ValueKind==JsonValueKind.String && item.Value.ValueKind==JsonValueKind.String &&
                !Regex.IsMatch(item.Value.GetString()??"",pattern.GetString()??"",
                    RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(200)))
                errors.Add(item.Name+": invalid identifier");
            if(item.Value.ValueKind==JsonValueKind.Array)
            {
                var itemRule=Get(Get(rule,"items"),"type");
                var kinds=itemRule.ValueKind switch
                {
                    JsonValueKind.String=>[itemRule.GetString()??""],
                    JsonValueKind.Array=>itemRule.EnumerateArray()
                        .Select(x=>x.GetString()??"").ToArray(),
                    _=>Array.Empty<string>()
                };
                foreach(var (index,element) in item.Value.EnumerateArray().Index())
                    if(kinds.Length>0 && !kinds.Any(t=>MatchesType(element,t)))
                        errors.Add($"{item.Name}[{index}]: invalid item type");
            }
        }
        var task=Text(r,"taskId");var spec=Text(r,"specification");
        if(task is null || !TaskId().IsMatch(task) || !tasks.TryGetValue(task,out var known))
            errors.Add("taskId: not present in public task registry");
        else if(spec is null || !SpecId().IsMatch(spec) || known!=spec)
            errors.Add("specification: does not match task registry");
        foreach(var field in new[]{"baseCommit","workingCommit"})
        {
            var value=Get(r,field);
            if(value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) &&
                (value.ValueKind!=JsonValueKind.String || !Sha().IsMatch(value.GetString()??"")))
                errors.Add(field+": must be a full lowercase Git SHA or null");
        }
        var repository=Get(r,"repository");
        if(repository.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) &&
            (repository.ValueKind!=JsonValueKind.String ||
             !RepoUrl().IsMatch(repository.GetString()??"")))
            errors.Add("repository: expected canonical public GitHub URL");
        var branch=Get(r,"branch");
        if(branch.ValueKind==JsonValueKind.String)
        {
            var value=branch.GetString()??"";
            if(string.IsNullOrWhiteSpace(value) || value.StartsWith('-') ||
                value.Contains("..",StringComparison.Ordinal) || value.Any(char.IsWhiteSpace))
                errors.Add("branch: invalid or unsafe ref");
        }
        var changed=Get(r,"changedFiles");
        if(changed.ValueKind==JsonValueKind.Array)
            foreach(var (i,item) in changed.EnumerateArray().Index())
            {
                if(item.ValueKind!=JsonValueKind.String)
                    continue; // schema permits structured change receipts in historical records
                var value=item.GetString()??"";
                if(value.Length==0 || value.StartsWith('/') ||
                   value.Contains('\\') || value.Split('/').Any(x=>x is "" or "." or ".."))
                    errors.Add($"changedFiles[{i}]: unsafe repository-relative path");
            }
        var status=Text(r,"status");
        if(status is not null && !ValidStatuses.Contains(status))
            errors.Add("status: unknown");
        if(status is "review" or "verified" && Text(r,"workingCommit") is null)
            errors.Add($"status={status}: workingCommit must be recorded");
        if(status=="review" && EmptyArray(Get(r,"pullRequests")))
            errors.Add("status=review: pullRequests cannot be empty");
        if(status=="verified" && EmptyArray(Get(r,"testResults")))
            errors.Add("status=verified: testResults cannot be empty");
        var effects=Get(r,"externalEffects");
        if(effects.ValueKind==JsonValueKind.Array)
        {
            var used=new HashSet<string>(StringComparer.Ordinal);
            foreach(var (i,effect) in effects.EnumerateArray().Index())
            {
                if(effect.ValueKind!=JsonValueKind.Object)continue;
                string prefix=$"externalEffects[{i}]: ";
                foreach(var key in new[]{"action","idempotencyKey","state","receipt"})
                    if(!effect.TryGetProperty(key,out _))errors.Add(prefix+"missing "+key);
                var keyText=Text(effect,"idempotencyKey");
                if(string.IsNullOrWhiteSpace(keyText))
                    errors.Add(prefix+"non-empty idempotencyKey required");
                else if(!used.Add(keyText))errors.Add(prefix+"duplicate idempotencyKey");
                var state=Text(effect,"state");
                if(state is null || !EffectStates.Contains(state))
                    errors.Add(prefix+"invalid state");
                if(state=="unknown")
                {
                    var step=Text(r,"nextSafeAction");
                    if(step is null || !ReconcileAction().IsMatch(step))
                        errors.Add(prefix+"Unknown needs a reconciliation step");
                    if(Get(effect,"receipt").ValueKind!=JsonValueKind.Null)
                        errors.Add(prefix+"Unknown cannot have a success receipt");
                }
                if(state=="succeeded" &&
                   Get(effect,"receipt").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    errors.Add(prefix+"success needs a receipt");
            }
        }
        var raw=r.GetRawText();
        if(SecretPattern().IsMatch(raw))
            errors.Add("possible credential/private key signature in checkpoint");
        void Walk(JsonElement element)
        {
            if(element.ValueKind==JsonValueKind.Object)
                foreach(var key in element.EnumerateObject())
                {
                    if(Sensitive.Contains(key.Name.ToLowerInvariant()))
                        errors.Add("forbidden sensitive field: "+key.Name);
                    Walk(key.Value);
                }
            else if(element.ValueKind==JsonValueKind.Array)
                foreach(var child in element.EnumerateArray())Walk(child);
        }
        Walk(r);
        return errors;
    }

    private static bool EmptyArray(JsonElement e)=>e.ValueKind!=JsonValueKind.Array || e.GetArrayLength()==0;
    private static string? Text(JsonElement e,string name)=>
        e.ValueKind==JsonValueKind.Object && e.TryGetProperty(name,out var x) &&
        x.ValueKind==JsonValueKind.String?x.GetString():null;
    private static JsonElement Get(JsonElement e,string name)=>
        e.ValueKind==JsonValueKind.Object && e.TryGetProperty(name,out var x)?x:default;
    private static bool MatchesType(JsonElement e,string type)=>type switch
    {
        "string"=>e.ValueKind==JsonValueKind.String,
        "null"=>e.ValueKind==JsonValueKind.Null,
        "object"=>e.ValueKind==JsonValueKind.Object,
        "array"=>e.ValueKind==JsonValueKind.Array,
        _=>false
    };
    private static Dictionary<string,string> ReadTasks(string path)
    {
        using var csv=new TextFieldParser(path){TextFieldType=FieldType.Delimited};
        csv.SetDelimiters(",");
        var columns=csv.ReadFields()??throw new InvalidDataException("Missing task headers");
        int tid=Array.IndexOf(columns,"Work ID"),sid=Array.IndexOf(columns,"Specification");
        if(tid<0 || sid<0)throw new InvalidDataException("Missing task/spec columns");
        var tasks=new Dictionary<string,string>(StringComparer.Ordinal);
        while(!csv.EndOfData)
        {
            var row=csv.ReadFields();
            if(row is null || row.Length!=columns.Length)
                throw new InvalidDataException("Malformed task registry CSV");
            if(!tasks.TryAdd(row[tid],row[sid]))
                throw new InvalidDataException("Duplicate task registry identifier");
        }
        return tasks;
    }
}
