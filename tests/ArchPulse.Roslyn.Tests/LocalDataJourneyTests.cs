// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class LocalDataJourneyTests
{
    private const string Fixture = """
    using System;
    namespace ArchPulse.DataJourney {
      [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method)]
      public sealed class SensitiveSourceAttribute : Attribute {}
      [AttributeUsage(AttributeTargets.Method)]
      public sealed class SensitiveSinkAttribute : Attribute {}
      [AttributeUsage(AttributeTargets.Method)]
      public sealed class DeclaredSanitizerAttribute : Attribute {}
    }
    namespace Example {
      using ArchPulse.DataJourney;
      public static class Flows {
        [SensitiveSink] public static void Sink(string value) {}
        [DeclaredSanitizer] public static string Sanitize(string x) => x; // lying, not trusted
        public static string Opaque(string x) => x;
        [SensitiveSource] public static string Source() => "secret-string-never-in-report";
        public static void Unsafe([SensitiveSource] string token) {
          var alias = token; Sink(alias);
        }
        public static void Sanitized([SensitiveSource] string token) {
          var alias = Sanitize(token); Sink(alias);
        }
        public static void NoCandidate([SensitiveSource] string token) {
          var safe = "public"; Sink(safe);
        }
        public static void UnknownBranch([SensitiveSource] string token, bool flag) {
          var alias = token; if (flag) alias = "other"; Sink(alias);
        }
        public static void UnknownOpaque([SensitiveSource] string token) {
          var alias = Opaque(token); Sink(alias);
        }
        public static void KnownSourceCall() {
          var alias = Source(); Sink(alias);
        }
        public static void Reassigned([SensitiveSource] string token) {
          var alias = token; alias = "public"; Sink(alias);
        }
        public static string ReturnSource([SensitiveSource] string token) {
          return token;
        }
        public static void Decoy([Fake.SensitiveSource] string token) {
          Sink(token);
        }
      }
    }
    namespace Fake {
      [AttributeUsage(AttributeTargets.Parameter)]
      public sealed class SensitiveSourceAttribute : Attribute {}
    }
    """;

    private static (GraphSnapshot Graph, LocalDataJourneyReport Report) Analyze(
        string revision = "rev", string source = Fixture)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "FlowCorpus.cs");
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => MetadataReference.CreateFromFile(f));
        var compilation = CSharpCompilation.Create("Corpus", [tree], trusted,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.DoesNotContain(compilation.GetDiagnostics(),
            d => d.Severity == DiagnosticSeverity.Error);
        var graph = StaticGraphIndexer.IndexCompilation("Corpus", compilation, revision);
        var report = LocalDataJourneyAnalyzer.Analyze(graph,
            [("Corpus", compilation)], Directory.GetCurrentDirectory());
        return (graph, report);
    }

    private static LocalDataFlowFinding Find(LocalDataJourneyReport report, string name) =>
        Assert.Single(report.Findings, f => f.MethodSymbolId.Contains(
            "." + name + "(", StringComparison.Ordinal) ||
            f.MethodSymbolId.EndsWith("." + name, StringComparison.Ordinal));

    [Fact]
    public void UnsafeAssignmentsAndExplicitSourceMethodsRetainExactSourceProof()
    {
        var (graph, report) = Analyze();
        LocalDataJourneyContract.Validate(graph, report);
        foreach (var name in new[] { "Unsafe", "KnownSourceCall" })
        {
            var finding = Find(report, name);
            Assert.Equal(LocalFlowOutcome.StaticPossibleRisk, finding.Outcome);
            Assert.Equal(LocalFlowDestination.DeclaredSink, finding.Destination);
            Assert.False(string.IsNullOrWhiteSpace(finding.SourceSymbolId));
            Assert.Equal("rev", finding.Location.Revision);
            Assert.Equal(EvidenceKind.Static, finding.Location.Kind);
            Assert.True(finding.Location.Line > 1);
        }
    }

    [Fact]
    public void DeclaredSanitizerIsNotAnApprovedPrivacyFix()
    {
        var (_, report) = Analyze();
        var finding = Find(report, "Sanitized");
        Assert.Equal(LocalFlowOutcome.DeclaredSanitizerNeedsReview, finding.Outcome);
        Assert.Equal("Unknown", report.RuntimeStatus);
        Assert.Equal(LocalDataJourneyContract.Scope, report.Scope);
    }

    [Fact]
    public void OpaqueCallBranchAndNameOnlyDecoyNeverBecomeSafe()
    {
        var (_, report) = Analyze();
        foreach (var name in new[] { "UnknownBranch", "UnknownOpaque", "Decoy" })
            Assert.Equal(LocalFlowOutcome.Unknown, Find(report, name).Outcome);
    }

    [Fact]
    public void ReassignmentsAndLiteralInputsAreLimitedToNoCandidateInSubset()
    {
        var (_, report) = Analyze();
        Assert.Equal(LocalFlowOutcome.NoCandidateInSubset, Find(report, "NoCandidate").Outcome);
        Assert.Equal(LocalFlowOutcome.NoCandidateInSubset, Find(report, "Reassigned").Outcome);
    }

    [Fact]
    public void ReturnHasDistinctProvenanceAndIsNotDeclaredExternalSink()
    {
        var (_, report) = Analyze();
        var finding = Find(report, "ReturnSource");
        Assert.Equal(LocalFlowDestination.MethodReturn, finding.Destination);
        Assert.Equal(LocalFlowOutcome.StaticReturnPropagation, finding.Outcome);
        Assert.Contains("::parameter:token", finding.SourceSymbolId);
    }

    [Fact]
    public void ReportContainsOnlyMetadataAndNoSourceLiterals()
    {
        var (_, report) = Analyze();
        var raw = System.Text.Json.JsonSerializer.Serialize(report);
        Assert.DoesNotContain("secret-string-never-in-report", raw);
        Assert.DoesNotContain("public\"", raw);
        Assert.DoesNotContain("privacy safe", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(9, report.Findings.Count);
    }

    [Fact]
    public void ExactRevisionStoreIsImmutableAndTamperingFailsClosed()
    {
        var (graph, report) = Analyze();
        using var temp = new Temp();
        var store = new LocalDataJourneyStore(temp.Path);
        Assert.Null(store.Load(graph));
        var path = store.Save(graph, report);
        Assert.Equal(path, store.Save(graph, report));
        Assert.Equal(report.Findings.Count, store.Load(graph)!.Findings.Count);
        var altered = report with { Findings = [] };
        Assert.Throws<InvalidOperationException>(() => store.Save(graph, altered));
        var (otherGraph, _) = Analyze("another");
        Assert.Null(store.Load(otherGraph));
        Assert.Throws<InvalidDataException>(() => store.Save(otherGraph, report));
        File.WriteAllText(path, File.ReadAllText(path).Replace("StaticPossibleRisk", "VerifiedPrivacySafe"));
        Assert.ThrowsAny<Exception>(() => store.Load(graph));
    }

    [Fact]
    public void ForgedEvidenceOrRuntimeClaimsAreRejected()
    {
        var (graph, report) = Analyze();
        Assert.Throws<InvalidDataException>(() => LocalDataJourneyContract.Validate(graph,
            report with { RuntimeStatus = "Observed" }));
        var fake = report.Findings[0] with
            { Location = report.Findings[0].Location with { Kind = EvidenceKind.Verified } };
        Assert.Throws<InvalidDataException>(() => LocalDataJourneyContract.Validate(graph,
            report with { Findings = [fake] }));
    }

    private sealed class Temp : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "archpulse-datajourney-tests-" +
            Guid.NewGuid().ToString("N"));
        public Temp() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
