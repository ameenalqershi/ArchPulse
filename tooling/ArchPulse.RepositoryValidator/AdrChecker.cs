// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace ArchPulse.RepositoryValidator;

/// <summary>Offline v1 ADR structural and lifecycle contract (S22).
/// Not a human reviewer/permission attestation, history audit, Mermaid parser or approval.</summary>
public static partial class AdrChecker
{
    private static readonly string[] ContractFields =
    [
        "schemaVersion", "id", "title", "status", "document", "source", "author",
        "dates", "affectedModules", "alternatives", "proposal", "reviewers",
        "approval", "supersessionApproval", "supersededBy", "gates",
        "gateEvidence", "migration", "verification"
    ];
    private static readonly (string Gate, string Flag)[] GateFlags =
    [
        ("security","securityRequired"), ("privacy","privacyRequired"),
        ("contract","contractRequired"), ("persistence","persistenceRequired")
    ];

    [GeneratedRegex(@"^ADR-[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex AdrId();
    [GeneratedRegex(@"^T[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskId();
    [GeneratedRegex(@"^S[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SpecId();
    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha();
    [GeneratedRegex(@"^https://github\.com/[^/]+/[^/]+/pull/[0-9]+#pullrequestreview-[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ReviewUrl();
    [GeneratedRegex(@"^docs/decisions/ADR-[0-9]{4}-[a-z0-9-]+\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex AdrDocument();
    [GeneratedRegex(@"^(flowchart|graph|sequenceDiagram|classDiagram|stateDiagram-v2|erDiagram|C4Context|C4Container|C4Component)(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex DiagramHead();

    public static IReadOnlyList<string> Validate(string root)
    {
        root = Path.GetFullPath(root);
        var errors = new List<string>();
        var schemaPath = Path.Combine(root, "docs/decisions/adr-v1.schema.json");
        try
        {
            using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
            var doc = schema.RootElement;
            if (Str(doc, "title") != "ArchPulse Architecture Decision Record v1")
                errors.Add("unsupported ADR schema");
            var required = Strings(Value(doc, "required"));
            var properties = Value(doc, "properties");
            if (!required.ToHashSet(StringComparer.Ordinal).SetEquals(ContractFields) ||
                properties.ValueKind != JsonValueKind.Object ||
                !required.ToHashSet(StringComparer.Ordinal).SetEquals(
                    properties.EnumerateObject().Select(p => p.Name)))
                errors.Add("ADR schema required and properties differ");
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return ["unreadable ADR JSON schema: " + e.Message];
        }
        Dictionary<string,string> tasks;
        try { tasks = ReadTasks(Path.Combine(root, "docs/requirements/task-registry.csv")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or MalformedLineException or InvalidDataException)
        {
            errors.Add("task traceability registry unavailable: " + e.Message);
            tasks = new(StringComparer.Ordinal);
        }
        var dir = Path.Combine(root, "docs/decisions/records");
        if (!Directory.Exists(dir)) return [..errors, "no machine-readable ADR records"];
        var records = new Dictionary<string,(string Status,string? Successor)>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(dir,"*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (new FileInfo(file).Length is < 1 or > 1048576)
                    throw new InvalidDataException("ADR record exceeds bounded file size");
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var record = doc.RootElement;
                CheckRecord(record, Path.GetFileName(file), root, tasks, errors);
                var id = Str(record,"id");
                if (id is not null && !records.TryAdd(id, (Str(record,"status")??"",Str(record,"supersededBy"))))
                    errors.Add("duplicate ADR ID " + id);
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add(Path.GetFileName(file) + ": invalid JSON: " + e.Message);
            }
        }
        if (records.Count==0) errors.Add("no machine-readable ADR records");
        foreach (var (id, pair) in records)
            if (pair.Status=="Superseded" &&
                (pair.Successor is null || !records.TryGetValue(pair.Successor,out var next) ||
                 next.Status!="Accepted"))
                errors.Add($"{id}: successor {pair.Successor ?? "(null)"} is not a recorded Accepted ADR");
        return errors;
    }

    private static void CheckRecord(JsonElement r,string file,string root,
        Dictionary<string,string> tasks,List<string> errors)
    {
        Require(r,ContractFields,file,errors);
        string? id=Str(r,"id"), status=Str(r,"status");
        if (id is null || !AdrId().IsMatch(id) || file!=id+".json")
            errors.Add(file+": ID/file mismatch or invalid ID");
        if (Str(r,"schemaVersion")!="1.0") errors.Add(file+": unsupported schemaVersion");
        if (!Enough(r,"title",8) || !Enough(r,"author",3)) errors.Add(file+": title/author required");
        if (!Enough(r,"proposal",15)) errors.Add(file+": proposal rationale required");
        if (status is not ("Proposed" or "Accepted" or "Superseded"))
            errors.Add(file+": invalid status");
        var source=Value(r,"source");
        Require(source,["specification","task","notion"],file+".source",errors);
        var spec=Str(source,"specification");var task=Str(source,"task");
        if (spec is null || !SpecId().IsMatch(spec)) errors.Add(file+": invalid specification");
        if (task is null || !TaskId().IsMatch(task) ||
            !tasks.TryGetValue(task,out var found) || found!=spec)
            errors.Add(file+": unknown or mismatched task/spec");
        if (!ValidUrl(Str(source,"notion"),"app.notion.com"))
            errors.Add(file+": invalid canonical Notion URL");
        var dates=Value(r,"dates");
        Require(dates,["proposed","accepted"],file+".dates",errors);
        var proposed=Str(dates,"proposed");var accepted=Str(dates,"accepted");
        if (!Date(proposed))errors.Add(file+": invalid proposal date");
        if (Value(dates,"accepted").ValueKind!=JsonValueKind.Null && !Date(accepted))
            errors.Add(file+": invalid acceptance date");
        ValidateList(Value(r,"affectedModules"),file+".affectedModules",errors,1);
        var reviewers=ValidateList(Value(r,"reviewers"),file+".reviewers",errors,0);
        var alternatives=Value(r,"alternatives");
        if (alternatives.ValueKind!=JsonValueKind.Array || alternatives.GetArrayLength()<2)
            errors.Add(file+": at least two alternatives required");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        var diagrams=new List<(string Id,string Diagram)>();
        if (alternatives.ValueKind==JsonValueKind.Array)
            foreach (var (i,alt) in alternatives.EnumerateArray().Index())
            {
                var loc=$"{file}.alternatives[{i}]";
                Require(alt,["id","label","evidence","tradeoffs","mermaid"],loc,errors);
                var aid=Str(alt,"id");
                if (aid is null || !Regex.IsMatch(aid,@"^[A-Z]$",RegexOptions.CultureInvariant))
                    errors.Add(loc+": invalid alternative ID");
                else if (!ids.Add(aid)) errors.Add(file+": duplicate alternative IDs");
                if (!Enough(alt,"label",3) || !Enough(alt,"tradeoffs",8))
                    errors.Add(loc+": missing label or tradeoffs");
                ValidateList(Value(alt,"evidence"),loc+".evidence",errors,1,true);
                var diagram=Str(alt,"mermaid");
                if (diagram is null || diagram.Trim().Length<20 ||
                    !DiagramHead().IsMatch(diagram) || diagram.Contains('\0') ||
                    diagram.Contains(new string((char)96,3),StringComparison.Ordinal))
                    errors.Add(loc+": unsupported or missing Mermaid graph");
                if (aid is not null && diagram is not null)diagrams.Add((aid,diagram));
            }
        var document=Str(r,"document");
        if (document is null || !AdrDocument().IsMatch(document) || id is null ||
            !document.StartsWith("docs/decisions/"+id+"-",StringComparison.Ordinal))
            errors.Add(file+": invalid or mismatched Markdown path");
        else
        {
            string md="";
            try {md=File.ReadAllText(Path.Combine(root,document)).Replace("\r\n","\n",StringComparison.Ordinal);}
            catch (Exception e) when(e is IOException or UnauthorizedAccessException)
            {errors.Add(file+": missing/unreadable narrative: "+e.Message);}
            if (!md.Contains(id,StringComparison.Ordinal))errors.Add(file+": narrative lacks ADR ID");
            if (!Regex.IsMatch(md,@"\b(Proposed|Accepted|Superseded)\b",RegexOptions.CultureInvariant))
                errors.Add(file+": narrative lacks status");
            string ticks=new string((char)96,3);
            foreach(var (aid,diagram) in diagrams)
            {
                if(!Regex.IsMatch(md,@"(?m)^## Alternative "+Regex.Escape(aid)+@"\b",
                    RegexOptions.CultureInvariant))
                    errors.Add(file+": alternative "+aid+" heading missing in narrative");
                if(!md.Contains(ticks+"mermaid\n"+diagram+"\n"+ticks,StringComparison.Ordinal))
                    errors.Add(file+": alternative "+aid+" diagram differs from Markdown");
            }
        }
        var gates=Value(r,"gates");var gateEvidence=Value(r,"gateEvidence");
        Require(gates,GateFlags.Select(x=>x.Flag).ToArray(),file+".gates",errors);
        Require(gateEvidence,GateFlags.Select(x=>x.Gate).ToArray(),file+".gateEvidence",errors);
        foreach(var (name,flag) in GateFlags)
        {
            var value=Value(gates,flag);
            if(value.ValueKind is not(JsonValueKind.True or JsonValueKind.False))
                errors.Add(file+": "+flag+" must be boolean");
            var links=ValidateList(Value(gateEvidence,name),file+".gateEvidence."+name,errors,0,true);
            if(status!="Proposed" && value.ValueKind==JsonValueKind.True && links.Count==0)
                errors.Add(file+": Accepted/Superseded requires "+name+" evidence");
        }
        var migration=Value(r,"migration");
        Require(migration,["forward","rollback","compatibility"],file+".migration",errors);
        foreach(var key in new[]{"forward","rollback","compatibility"})
            if(!Enough(migration,key,10))errors.Add(file+": missing migration."+key);
        var verification=Value(r,"verification");
        Require(verification,["tests","ciUrl","graphImpact"],file+".verification",errors);
        ValidateList(Value(verification,"tests"),file+".verification.tests",errors,1);
        if(!Enough(verification,"graphImpact",10))errors.Add(file+": graph impact must be recorded");
        var ci=Str(verification,"ciUrl");
        if(Value(verification,"ciUrl").ValueKind!=JsonValueKind.Null && !ValidUrl(ci,"github.com"))
            errors.Add(file+": invalid CI link");
        var approval=Value(r,"approval");var supersession=Value(r,"supersessionApproval");
        var replacement=Value(r,"supersededBy");
        if(status=="Proposed")
        {
            if(approval.ValueKind!=JsonValueKind.Null || supersession.ValueKind!=JsonValueKind.Null ||
                replacement.ValueKind!=JsonValueKind.Null ||
                Value(dates,"accepted").ValueKind!=JsonValueKind.Null)
                errors.Add(file+": Proposed cannot claim approval, acceptance or supersession");
        }
        else if(status is "Accepted" or "Superseded")
        {
            var review=ValidateReview(approval,file+".approval",errors);
            if(review.Reviewer is null || !reviewers.Contains(review.Reviewer))
                errors.Add(file+": approval reviewer must be declared");
            if(review.Reviewer?.Equals(Str(r,"author"),StringComparison.OrdinalIgnoreCase)==true)
                errors.Add(file+": self-approval not allowed");
            if(!Date(accepted))errors.Add(file+": missing accepted date");
            else if(Date(proposed) && string.CompareOrdinal(accepted,proposed)<0)
                errors.Add(file+": acceptance precedes proposal");
            if(ci is null)errors.Add(file+": Accepted/Superseded must link CI");
            if(status=="Accepted")
            {
                if(replacement.ValueKind!=JsonValueKind.Null ||
                    supersession.ValueKind!=JsonValueKind.Null)
                    errors.Add(file+": Accepted cannot claim supersession");
            }
            else
            {
                var successorReview=ValidateReview(supersession,file+".supersessionApproval",errors);
                if(successorReview.Reviewer is null || !reviewers.Contains(successorReview.Reviewer))
                    errors.Add(file+": supersession reviewer must be declared");
                if(successorReview.Url==review.Url || successorReview.Commit==review.Commit)
                    errors.Add(file+": supersession requires a distinct revision and review");
                if(Date(successorReview.Date) && Date(accepted) &&
                    string.CompareOrdinal(successorReview.Date,accepted)<0)
                    errors.Add(file+": supersession review predates acceptance");
                if(successorReview.Reviewer?.Equals(Str(r,"author"),StringComparison.OrdinalIgnoreCase)==true)
                    errors.Add(file+": supersession self-approval not allowed");
                var next=Str(r,"supersededBy");
                if(next is null || !AdrId().IsMatch(next) || next==id)
                    errors.Add(file+": Superseded requires a different successor ADR");
            }
        }
    }

    private static (string? Url,string? Reviewer,string? Commit,string? Date) ValidateReview(
        JsonElement value,string loc,List<string> errors)
    {
        Require(value,["reviewUrl","reviewer","commitSha","date","decision"],loc,errors);
        var url=Str(value,"reviewUrl");var user=Str(value,"reviewer");
        var sha=Str(value,"commitSha");var date=Str(value,"date");
        if(url is null || !ReviewUrl().IsMatch(url))errors.Add(loc+": expected GitHub review URL");
        if(string.IsNullOrWhiteSpace(user))errors.Add(loc+": missing reviewer login");
        if(sha is null || !Sha().IsMatch(sha))errors.Add(loc+": invalid commit SHA");
        if(!Date(date))errors.Add(loc+": invalid date");
        if(Str(value,"decision")!="Approved")errors.Add(loc+": only explicit Approved review is accepted");
        return(url,user,sha,date);
    }

    private static Dictionary<string,string> ReadTasks(string path)
    {
        using var parser=new TextFieldParser(path)
        {TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true};
        parser.SetDelimiters(",");
        var columns=parser.ReadFields()??throw new InvalidDataException("Missing task CSV headers");
        int work=Array.IndexOf(columns,"Work ID"),spec=Array.IndexOf(columns,"Specification");
        if(work<0 || spec<0)throw new InvalidDataException("Missing task/spec columns");
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        while(!parser.EndOfData)
        {
            var row=parser.ReadFields();
            if(row is null || row.Length!=columns.Length)
                throw new InvalidDataException("Malformed task CSV");
            if(!result.TryAdd(row[work],row[spec]))
                throw new InvalidDataException("Duplicate registry task ID");
        }
        return result;
    }

    private static JsonElement Value(JsonElement parent,string key)=>
        parent.ValueKind==JsonValueKind.Object && parent.TryGetProperty(key,out var val)?
        val:default;
    private static string? Str(JsonElement parent,string key)
    {
        var val=Value(parent,key);
        return val.ValueKind==JsonValueKind.String?val.GetString():null;
    }
    private static bool Enough(JsonElement parent,string key,int minimum)=>
        (Str(parent,key)?.Trim().Length??0)>=minimum;
    private static bool Date(string? date)=>date is not null &&
        Regex.IsMatch(date,@"^[0-9]{4}-[0-9]{2}-[0-9]{2}$",RegexOptions.CultureInvariant) &&
        DateOnly.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,
            DateTimeStyles.None,out _);
    private static bool ValidUrl(string? value,string? host=null)=>
        value is not null && !value.Any(char.IsWhiteSpace) &&
        Uri.TryCreate(value,UriKind.Absolute,out var url) && url.Scheme=="https" &&
        !string.IsNullOrEmpty(url.Host) && string.IsNullOrEmpty(url.UserInfo) &&
        (host is null || url.Host.Equals(host,StringComparison.OrdinalIgnoreCase));

    private static List<string> Strings(JsonElement value)=>
        value.ValueKind==JsonValueKind.Array?
        value.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String)
            .Select(v=>v.GetString()??"").ToList():[];

    private static List<string> ValidateList(JsonElement value,string loc,
        List<string> errors,int minimum,bool urls=false)
    {
        var list=Strings(value);
        if(value.ValueKind!=JsonValueKind.Array ||
           list.Count!=(value.ValueKind==JsonValueKind.Array?value.GetArrayLength():0) ||
           list.Count<minimum || list.Any(v=>string.IsNullOrWhiteSpace(v) ||
                                             (urls && !ValidUrl(v))))
            errors.Add($"{loc}: expected >= {minimum} nonempty {(urls?"https URLs":"strings")}");
        if(list.Distinct(StringComparer.Ordinal).Count()!=list.Count)
            errors.Add(loc+": duplicate entries");
        return list;
    }
    private static void Require(JsonElement item,string[] keys,string loc,List<string> errors)
    {
        if(item.ValueKind!=JsonValueKind.Object)
        {errors.Add(loc+": expected object");return;}
        var required=keys.ToHashSet(StringComparer.Ordinal);
        var found=item.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
        if(!found.IsSupersetOf(required))
            errors.Add(loc+": missing "+string.Join(",",required.Except(found)));
        if(!required.IsSupersetOf(found))
            errors.Add(loc+": unexpected "+string.Join(",",found.Except(required)));
    }
}
