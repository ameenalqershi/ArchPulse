// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;
using Xunit;

namespace ArchPulse.Tooling.Tests;

/// <summary>Regression guards for the native replacements of retired scripts;
/// passing contracts do not authenticate remote effects or product acceptance.</summary>
public sealed class NativeToolRetirementTests
{
    private static readonly string Root=RepositoryRoot.Find(AppContext.BaseDirectory);

    [Fact]
    public void CommittedS26LockPassesNativeBaseline()=>
        Assert.Empty(RequirementBaselineChecker.Validate(Root));

    [Theory]
    [InlineData("schema","fake")]
    [InlineData("designStatus","implemented")]
    [InlineData("sourceSpecification","S00")]
    public void MutatedRequirementLockMetadataIsRejected(string field,string content)
    {
        using var f=new BaselineFixture();
        f.ChangeLock(x=>x[field]=content);
        Assert.NotEmpty(RequirementBaselineChecker.Validate(f.Path));
    }

    [Fact]
    public void TamperedRequirementIdFailsEvenWithSameRowCount()
    {
        using var f=new BaselineFixture();
        var csv=f.Requirements;
        var s=File.ReadAllText(csv);
        var id=File.ReadLines(csv).Skip(1).First().Split('"')[1];
        Assert.StartsWith("R-",id,StringComparison.Ordinal);
        File.WriteAllText(csv,s.Replace(id,"R-UNREVIEWED-999",StringComparison.Ordinal));
        Assert.Contains(RequirementBaselineChecker.Validate(f.Path),
            e=>e.Contains("requirement ID drift",StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateRequirementTaskAndUnreviewedImplementationFailClosed()
    {
        using var f=new BaselineFixture();
        var s=File.ReadAllText(f.Requirements);
        // CSV data contains direct comma-separated task references.
        var row=s.Split('\n').FirstOrDefault(x=>x.Contains("T001,T111",StringComparison.Ordinal));
        Assert.NotNull(row);
        File.WriteAllText(f.Requirements,s.Replace("T001,T111","T001,T001",StringComparison.Ordinal));
        Assert.Contains(RequirementBaselineChecker.Validate(f.Path),
            e=>e.Contains("duplicate task references",StringComparison.Ordinal));
        f.ResetRequirements();
        File.WriteAllText(f.Requirements,File.ReadAllText(f.Requirements)
            .Replace("\"Planned\"","\"Verified\"",StringComparison.Ordinal));
        Assert.Contains(RequirementBaselineChecker.Validate(f.Path),
            e=>e.Contains("unsupported implementation claim",StringComparison.Ordinal));
    }

    [Fact]
    public void WrongFamilySpecAndMissingS33TraceFail()
    {
        using var f=new BaselineFixture();
        f.ChangeLock(x=>x["familyPrimarySpecifications"]!["R-TJ"]="S01");
        Assert.Contains(RequirementBaselineChecker.Validate(f.Path),
            e=>e.Contains("S27-S33 family mappings",StringComparison.Ordinal));
    }

    [Fact]
    public void MissingRequirementLockFailsClosed()
    {
        using var f=new BaselineFixture();
        File.Delete(f.LockPath);
        Assert.Contains(RequirementBaselineChecker.Validate(f.Path),
            e=>e.Contains("unreadable lock",StringComparison.Ordinal));
    }

    [Fact]
    public void AllCommittedAgentHandoffsPassNativeChecks()=>
        Assert.Empty(HandoffAllChecker.Validate(Root));

    [Fact]
    public void UnauthorizedStatusAndUnexpectedFieldFail()
    {
        using var f=new HandoffFixture();
        f.Mutate(x=>{x["status"]="issued";x["unknownKey"]="unapproved";});
        var errors=HandoffAllChecker.Validate(f.Path);
        Assert.Contains(errors,e=>e.Contains("status",StringComparison.Ordinal));
        Assert.Contains(errors,e=>e.Contains("unrecognized field",StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidShaAndUnknownTaskFail()
    {
        using var f=new HandoffFixture();
        f.Mutate(x=>{x["baseCommit"]="main";x["taskId"]="T999";});
        var errors=HandoffAllChecker.Validate(f.Path);
        Assert.Contains(errors,e=>e.Contains("Git SHA",StringComparison.Ordinal));
        Assert.Contains(errors,e=>e.Contains("taskId",StringComparison.Ordinal));
    }

    [Fact]
    public void SecretsInValuesAndNestedKeysAreRejected()
    {
        using var f=new HandoffFixture();
        f.Mutate(x=> {
            x["lastVerifiedAction"]="ghp_"+new string('A',35);
            x["externalEffects"]=new JsonArray(new JsonObject{
                ["state"]="unknown",["idempotencyKey"]="safe-id",["action"]="noop",
                ["receipt"]=null,["api_key"]="not-allowed"
            });
        });
        var errors=HandoffAllChecker.Validate(f.Path);
        Assert.Contains(errors,e=>e.Contains("credential/private key",StringComparison.Ordinal));
        Assert.Contains(errors,e=>e.Contains("sensitive field",StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateEffectKeysAndSuccessWithoutReceiptFail()
    {
        using var f=new HandoffFixture();
        f.Mutate(x=> {
            var effect=new JsonObject{
                ["action"]="label",["idempotencyKey"]="duplicate",["state"]="succeeded",["receipt"]=null
            };
            x["externalEffects"]=new JsonArray(effect.DeepClone(),effect.DeepClone());
        });
        var errors=HandoffAllChecker.Validate(f.Path);
        Assert.Contains(errors,e=>e.Contains("duplicate idempotencyKey",StringComparison.Ordinal));
        Assert.Contains(errors,e=>e.Contains("success needs a receipt",StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownEffectRequiresExplicitReconciliationAndNoReceipt()
    {
        using var f=new HandoffFixture();
        f.Mutate(x=> {
            x["nextSafeAction"]="continue normally";
            x["externalEffects"]=new JsonArray(new JsonObject{
                ["action"]="publish",["idempotencyKey"]="one",["state"]="unknown",["receipt"]="claimed"
            });
        });
        var errors=HandoffAllChecker.Validate(f.Path);
        Assert.Contains(errors,e=>e.Contains("reconciliation step",StringComparison.Ordinal));
        Assert.Contains(errors,e=>e.Contains("Unknown cannot have a success receipt",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("/root/secret")]
    [InlineData("src//test.cs")]
    public void UnsafeChangedPathsAreRejected(string badPath)
    {
        using var f=new HandoffFixture();
        f.Mutate(x=>x["changedFiles"]=new JsonArray(badPath));
        Assert.Contains(HandoffAllChecker.Validate(f.Path),
            e=>e.Contains("unsafe repository-relative path",StringComparison.Ordinal));
    }

    [Fact]
    public void MissingCheckpointAndMalformedJsonFailClosed()
    {
        using var f=new HandoffFixture();
        File.WriteAllText(f.RecordPath,"{bad");
        Assert.Contains(HandoffAllChecker.Validate(f.Path),
            e=>e.Contains("invalid checkpoint",StringComparison.Ordinal));
        var folder=System.IO.Path.GetDirectoryName(f.RecordPath)!;
        foreach(var foundation in Directory.GetFiles(folder,"FOUNDATION*.json"))
            File.Delete(foundation);
        Assert.Contains(HandoffAllChecker.Validate(f.Path),
            e=>e.Contains("missing public foundation checkpoint",StringComparison.Ordinal));
    }

    private sealed class BaselineFixture : IDisposable
    {
        public string Path {get;}=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-s26-native-"+Guid.NewGuid().ToString("N"));
        public string LockPath=>System.IO.Path.Combine(Path,"docs/requirements/requirement-baseline-lock.json");
        public string Requirements=>System.IO.Path.Combine(Path,"docs/requirements/requirements.csv");
        public BaselineFixture()
        {
            var dir=System.IO.Path.Combine(Path,"docs/requirements");
            Directory.CreateDirectory(dir);
            foreach(var name in new[]{"requirement-baseline-lock.json","requirements.csv","task-registry.csv"})
                File.Copy(System.IO.Path.Combine(Root,"docs/requirements",name),
                    System.IO.Path.Combine(dir,name));
        }
        public void ChangeLock(Action<JsonObject> change)
        {
            var o=JsonNode.Parse(File.ReadAllText(LockPath))!.AsObject();
            change(o);File.WriteAllText(LockPath,o.ToJsonString());
        }
        public void ResetRequirements()=>File.Copy(System.IO.Path.Combine(Root,
            "docs/requirements/requirements.csv"),Requirements,true);
        public void Dispose()=>Directory.Delete(Path,true);
    }

    private sealed class HandoffFixture : IDisposable
    {
        public string Path {get;}=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-s25-native-"+Guid.NewGuid().ToString("N"));
        public string RecordPath=>System.IO.Path.Combine(Path,
            "docs/operations/agent-handoff/FOUNDATION-AUDIT-2026-11-10.json");
        public HandoffFixture()
        {
            var dir=System.IO.Path.GetDirectoryName(RecordPath)!;
            Directory.CreateDirectory(dir);
            foreach(var file in Directory.GetFiles(System.IO.Path.Combine(Root,
                "docs/operations/agent-handoff"),"*.json"))
                File.Copy(file,System.IO.Path.Combine(dir,System.IO.Path.GetFileName(file)));
            var req=System.IO.Path.Combine(Path,"docs/requirements");
            Directory.CreateDirectory(req);
            File.Copy(System.IO.Path.Combine(Root,"docs/requirements/task-registry.csv"),
                System.IO.Path.Combine(req,"task-registry.csv"));
        }
        public void Mutate(Action<JsonObject> change)
        {
            var o=JsonNode.Parse(File.ReadAllText(RecordPath))!.AsObject();
            change(o);File.WriteAllText(RecordPath,o.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        }
        public void Dispose()=>Directory.Delete(Path,true);
    }
}
