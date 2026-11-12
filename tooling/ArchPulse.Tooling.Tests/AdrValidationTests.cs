// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;
using Xunit;

namespace ArchPulse.Tooling.Tests;

public sealed class AdrValidationTests
{
    private static readonly string RepoRoot = RepositoryRoot.Find(AppContext.BaseDirectory);

    [Fact]
    public void CurrentAdrsPassNativeLifecycleContract() =>
        Assert.Empty(AdrChecker.Validate(RepoRoot));

    [Fact]
    public void ValidProposedIsNotHumanApproved()
    {
        using var f = new Fixture();
        Assert.Empty(f.Errors());
        Assert.Equal("Proposed", f.Record()["status"]?.ToString());
    }

    [Fact]
    public void MissingAndUnknownTopLevelFieldsAreRejected()
    {
        using var f = new Fixture();
        f.Mutate(o => { o.Remove("title"); o["secretToggle"] = true; });
        AssertError(f,"missing");
        AssertError(f,"unexpected");
    }

    [Theory]
    [InlineData("source","task","T999","unknown or mismatched task/spec")]
    [InlineData("source","specification","S01","unknown or mismatched task/spec")]
    [InlineData("dates","proposed","2026-02-31","invalid proposal date")]
    [InlineData("dates","proposed","02/11/2026","invalid proposal date")]
    [InlineData("source","notion","https://evil.example/p/xxx","canonical Notion URL")]
    public void RejectsBrokenSourceProvenance(string group,string field,string value,string expected)
    {
        using var f = new Fixture();
        f.Mutate(x => x[group]![field] = value);
        AssertError(f,expected);
    }

    [Fact]
    public void RejectsMissingAlternativeAndDuplicateIDs()
    {
        using var f = new Fixture();
        f.Mutate(x => x["alternatives"]!.AsArray().RemoveAt(1));
        AssertError(f,"at least two alternatives");
        f.Reset();
        f.Mutate(x => x["alternatives"]![1]!["id"] = "A");
        AssertError(f,"duplicate alternative IDs");
    }

    [Theory]
    [InlineData("nonsense mermaid diagram","unsupported or missing Mermaid graph")]
    [InlineData("flowchart LR\n A-->B\n B-->NeverShown","diagram differs from Markdown")]
    public void RejectsMermaidFailures(string diagram,string message)
    {
        using var f = new Fixture();
        f.Mutate(o => o["alternatives"]![0]!["mermaid"] = diagram);
        AssertError(f,message);
    }

    [Fact]
    public void RejectsMissingNarrativeTraversalAndDrift()
    {
        using var f = new Fixture();
        f.DeleteNarrative();
        AssertError(f,"missing/unreadable narrative");
        f.Reset();
        f.Mutate(x => x["document"]="../secret.md");
        AssertError(f,"invalid or mismatched Markdown path");
    }

    [Fact]
    public void ProposalCannotCarryForgedApproval()
    {
        using var f = new Fixture();
        f.Mutate(x => x["approval"] = Receipt("reviewer-a"));
        AssertError(f,"Proposed cannot claim approval");
    }

    [Fact]
    public void SyntheticAcceptedShapePassesSyntaxButNotActualHumanAttestation()
    {
        using var f = new Fixture();
        f.Mutate(Accept);
        Assert.Empty(f.Errors());
    }

    [Fact]
    public void AcceptedWithoutReviewReceiptIsRejected()
    {
        using var f = new Fixture();
        f.Mutate(x => x["status"] = "Accepted");
        AssertError(f,"expected object");
    }

    [Fact]
    public void AcceptedMissingCiReceiptIsRejected()
    {
        using var f = new Fixture();
        f.Mutate(x => {Accept(x); x["verification"]!["ciUrl"]=null;});
        AssertError(f,"must link CI");
    }

    [Theory]
    [InlineData("Commented","explicit Approved")]
    [InlineData("Approved","self-approval")]
    public void AcceptedApprovalChecksAreEnforced(string decision,string expected)
    {
        using var f = new Fixture();
        f.Mutate(x => {
            Accept(x);
            x["approval"]!["decision"]=decision;
            if (decision=="Approved")x["author"]="reviewer-a";
        });
        AssertError(f,expected);
    }

    [Fact]
    public void RequiredSecurityReviewMustHaveEvidence()
    {
        using var f = new Fixture();
        f.Mutate(x => { Accept(x); x["gates"]!["securityRequired"]=true; });
        AssertError(f,"requires security evidence");
    }

    [Fact]
    public void SupersessionWithoutAcceptedSuccessorIsRejected()
    {
        using var f = new Fixture();
        f.Mutate(x => {
            Accept(x); x["status"]="Superseded";
            x["supersessionApproval"]=Receipt("reviewer-a",2);
            x["supersededBy"]="ADR-0099";
        });
        AssertError(f,"not a recorded Accepted ADR");
    }

    [Fact]
    public void SupersessionReviewMustBeDistinctAndListed()
    {
        using var f = new Fixture();
        f.Mutate(x => {
            Accept(x); x["status"]="Superseded";
            x["supersessionApproval"]=Receipt("reviewer-a");
            x["supersededBy"]="ADR-0099";
        });
        AssertError(f,"distinct revision and review");
        f.Reset();
        f.Mutate(x => {
            Accept(x);x["status"]="Superseded";
            x["supersessionApproval"]=Receipt("unlisted-reviewer",2);
            x["supersededBy"]="ADR-0099";
        });
        AssertError(f,"supersession reviewer must be declared");
    }

    [Fact]
    public void ValidSyntacticSupersessionToDifferentAcceptedRecord()
    {
        using var f = new Fixture();
        var successor = f.Record();
        successor["id"]="ADR-0012";
        successor["document"]="docs/decisions/ADR-0012-governance-records.md";
        Accept(successor);
        f.WriteExtra("ADR-0012",successor);
        f.Mutate(x=>{
            Accept(x);x["status"]="Superseded";
            x["supersessionApproval"]=Receipt("reviewer-a",2);
            x["supersededBy"]="ADR-0012";
        });
        Assert.Empty(f.Errors());
    }

    [Fact]
    public void MalformedDocumentAndMissingRegistryFailClosed()
    {
        using var f = new Fixture();
        File.WriteAllText(f.RecordPath,"not json");
        AssertError(f,"invalid JSON");
        f.Reset();
        File.Delete(Path.Combine(f.Root,"docs/requirements/task-registry.csv"));
        AssertError(f,"task traceability registry unavailable");
    }

    [Fact]
    public void NoRecordsFailsClosed()
    {
        using var f = new Fixture();
        File.Delete(f.RecordPath);
        AssertError(f,"no machine-readable ADR records");
    }

    private static void AssertError(Fixture f,string needle) =>
        Assert.Contains(f.Errors(),error=>error.Contains(needle,StringComparison.Ordinal));

    private static JsonNode Receipt(string reviewer,int version=1) =>
        JsonNode.Parse(JsonSerializer.Serialize(new {
            reviewUrl="https://github.com/example/archpulse/pull/42#pullrequestreview-"+(122+version),
            reviewer,commitSha=new string((char)('0'+version),40),
            date=version==1?"2026-11-11":"2026-11-12",
            decision="Approved"
        }))!;

    private static void Accept(JsonObject record)
    {
        record["status"]="Accepted";
        record["dates"]!["accepted"]="2026-11-11";
        record["reviewers"]=new JsonArray("reviewer-a");
        record["approval"]=Receipt("reviewer-a");
        record["verification"]!["ciUrl"]="https://github.com/example/archpulse/actions/runs/42";
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; }=Path.Combine(Path.GetTempPath(),
            "archpulse-native-adr-"+Guid.NewGuid().ToString("N"));
        public string RecordPath => Path.Combine(Root,"docs/decisions/records/ADR-0011.json");
        private string MarkdownPath => Path.Combine(Root,"docs/decisions/ADR-0011-governance-records.md");

        public Fixture()
        {
            foreach(var name in new[] {
                "docs/requirements/task-registry.csv",
                "docs/decisions/adr-v1.schema.json",
                "docs/decisions/records/ADR-0011.json",
                "docs/decisions/ADR-0011-governance-records.md"
            })
            {
                var dest=Path.Combine(Root,name);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(Path.Combine(RepoRoot,name),dest);
            }
        }
        public JsonObject Record()=>JsonNode.Parse(File.ReadAllText(RecordPath))!.AsObject();
        public void Mutate(Action<JsonObject> change)
        {
            var o=Record();change(o);
            File.WriteAllText(RecordPath,o.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        }
        public void Reset()
        {
            File.Copy(Path.Combine(RepoRoot,"docs/decisions/records/ADR-0011.json"),RecordPath,true);
            File.Copy(Path.Combine(RepoRoot,"docs/decisions/ADR-0011-governance-records.md"),MarkdownPath,true);
        }
        public void WriteExtra(string id,JsonNode value)
        {
            var dst=Path.Combine(Root,"docs/decisions/records",id+".json");
            File.WriteAllText(dst,value.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
            string markdown=File.ReadAllText(MarkdownPath).Replace("ADR-0011",id,StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(Root,"docs/decisions",id+"-governance-records.md"),markdown);
        }
        public void DeleteNarrative()=>File.Delete(MarkdownPath);
        public IReadOnlyList<string> Errors()=>AdrChecker.Validate(Root);
        public void Dispose()=>Directory.Delete(Root,true);
    }
}
