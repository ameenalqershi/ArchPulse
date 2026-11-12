// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.Graph;
using ArchPulse.TestJourney;
using Xunit;

namespace ArchPulse.TestJourney.Tests;

public sealed class TrxSourceCandidateMatcherTests
{
    private const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string TestIdA = "11111111-1111-4111-8111-111111111111";
    private const string TestIdB = "22222222-2222-4222-8222-222222222222";
    private const string ExecIdA = "33333333-3333-4333-8333-333333333333";
    private const string ExecIdB = "44444444-4444-4444-8444-444444444444";
    private const string SymbolId = "Tests.One::M:Acme.Tests.CalculatorTests.ShouldWork";
    private const string TargetId = "Sut::M:Acme.Calculator.Run";
    private static readonly SourceEvidence Evidence =
        new(ShaA, "Tests/CalculatorTests.cs", 6, 3, EvidenceKind.Static);

    [Fact]
    public void ExactNameSameDeclaredShaIsUnverifiedCandidateNotSutOutcome()
    {
        using var input = new TempTrx(Trx());
        var (graph, declarations) = Source();
        var imported = TrxEvidenceReader.Import(input.Path, ShaA, ShaA);
        var report = TrxSourceCandidateMatcher.Reconcile(graph, declarations, imported);
        Assert.Equal(TrxSourceCandidateMatcher.Schema, report.Schema);
        Assert.Equal("MatchingDeclaredRevisionUnverified", report.RevisionAssessment);
        Assert.Equal("CallerProvidedUnverified", report.RevisionEvidence);
        Assert.Equal("NoTestToSutRuntimeProof", report.SutOutcomeEvidence);
        Assert.Equal(1, report.UniqueNameCandidateAttempts);
        var candidate = Assert.Single(report.Attempts);
        Assert.Equal("UniqueNameCandidateUnverified", candidate.MatchStatus);
        Assert.Equal(SymbolId, candidate.CandidateTestMethodSymbolId);
        Assert.Equal("Tests.One", candidate.CandidateTestProject);
        Assert.Equal(Evidence, candidate.CandidateEvidence);
        Assert.Equal("Passed", candidate.RunnerOutcome);
        Assert.Equal("RunnerAttemptOnly", candidate.OutcomeScope);
        Assert.Equal("Unobserved", candidate.SutExecutionEvidence);
        Assert.False(candidate.PerTestCoverageProven);
        Assert.Equal(TestIdA, candidate.TestCaseId);
        Assert.Equal(imported.TrxSha256, report.TrxSha256);
    }

    [Theory]
    [InlineData(null, ShaA, "MissingDeclaredRevision")]
    [InlineData(ShaB, ShaA, "MismatchedDeclaredRevision")]
    [InlineData(ShaA, null, "MissingDeclaredRevision")]
    public void MissingOrStaleRevisionNeverAttachesEvenExactName(
        string? tested, string? graphRevision, string expectedAssessment)
    {
        using var input = new TempTrx(Trx());
        var (graph, declarations) = Source();
        var imported = TrxEvidenceReader.Import(input.Path, tested, graphRevision);
        var report = TrxSourceCandidateMatcher.Reconcile(graph, declarations, imported);
        Assert.Equal(expectedAssessment, report.RevisionAssessment);
        Assert.Equal("RevisionNotCorrelatable", Assert.Single(report.Attempts).MatchStatus);
        Assert.Null(report.Attempts[0].CandidateTestMethodSymbolId);
        Assert.Null(report.Attempts[0].CandidateEvidence);
        Assert.Equal(0, report.UniqueNameCandidateAttempts);
    }

    [Fact]
    public void SameClassAndMethodAcrossProjectsRemainAmbiguous()
    {
        using var input = new TempTrx(Trx());
        var (graph, declarations) = Source(duplicate: true);
        var result = TrxSourceCandidateMatcher.Reconcile(graph, declarations,
            TrxEvidenceReader.Import(input.Path, ShaA, ShaA));
        var candidate = Assert.Single(result.Attempts);
        Assert.Equal("AmbiguousExactName", candidate.MatchStatus);
        Assert.Equal(2, candidate.CandidateCount);
        Assert.Null(candidate.CandidateTestMethodSymbolId);
        Assert.Equal(1, result.AmbiguousNameAttempts);
    }

    [Theory]
    [InlineData("Acme.Tests.OtherTests", "ShouldWork")]
    [InlineData("Acme.Tests.CalculatorTests", "ShouldNotWork")]
    [InlineData("acme.Tests.CalculatorTests", "ShouldWork")]
    public void OnlyExactCaseSensitiveClassAndMethodAreCandidates(string cls, string method)
    {
        using var input = new TempTrx(Trx(cls: cls, method: method));
        var (graph, declarations) = Source();
        var candidate = Assert.Single(TrxSourceCandidateMatcher.Reconcile(graph, declarations,
            TrxEvidenceReader.Import(input.Path, ShaA, ShaA)).Attempts);
        Assert.Equal("NoExactSourceCandidate", candidate.MatchStatus);
        Assert.Null(candidate.CandidateTestMethodSymbolId);
    }

    [Fact]
    public void MissingDefinitionNeverGuessesAndNeverLeaksRawSensitiveOutput()
    {
        using var input = new TempTrx(Trx(withDefinition: false));
        var (graph, declarations) = Source();
        var result = TrxSourceCandidateMatcher.Reconcile(graph, declarations,
            TrxEvidenceReader.Import(input.Path, ShaA, ShaA));
        Assert.Equal("MissingOrUnsupportedRunnerName", Assert.Single(result.Attempts).MatchStatus);
        string serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("supersecret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("creditcard", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-error", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterizedAndRetriedRunnerCasesRemainSeparateAttempts()
    {
        string results = Result(TestIdA, ExecIdA, "Passed") +
                         Result(TestIdA, ExecIdB, "Failed") +
                         Result(TestIdB, ExecIdA, "NotExecuted");
        using var input = new TempTrx(Trx(results: results, twoDefinitions: true));
        var (graph, declarations) = Source();
        var report = TrxSourceCandidateMatcher.Reconcile(graph, declarations,
            TrxEvidenceReader.Import(input.Path, ShaA, ShaA));
        Assert.Equal(3, report.TotalAttempts);
        Assert.Equal(3, report.UniqueNameCandidateAttempts);
        Assert.Equal(2, report.Attempts.Select(a => a.TestCaseId).Distinct().Count());
        Assert.Equal(3, report.Attempts.Select(a => a.AttemptId).Distinct().Count());
        Assert.All(report.Attempts, x => Assert.Equal(SymbolId, x.CandidateTestMethodSymbolId));
        Assert.Equal(new[] { "Passed", "Failed", "Skipped" },
            report.Attempts.Select(a => a.RunnerOutcome).ToArray());
    }

    [Fact]
    public void UnsupportedSourceGenericMethodIsNotGuessed()
    {
        using var input = new TempTrx(Trx());
        var (graph, declarations) = Source();
        var badId = "Tests.One::M:Acme.Tests.CalculatorTests.ShouldWork#generic";
        var changed = graph with
        {
            Nodes = graph.Nodes.Select(n => n.SymbolId == SymbolId ? n with { SymbolId = badId } : n).ToArray(),
            Edges = graph.Edges.Select(e => e with
            {
                FromSymbolId = e.FromSymbolId == SymbolId ? badId : e.FromSymbolId
            }).ToArray()
        };
        var report = declarations with
        {
            TestDeclarations = declarations.TestDeclarations.Select(t =>
                t with { TestMethodSymbolId = badId }).ToArray(),
            Associations = declarations.Associations.Select(l =>
                l with { TestMethodSymbolId = badId }).ToArray()
        };
        var outcome = TrxSourceCandidateMatcher.Reconcile(changed, report,
            TrxEvidenceReader.Import(input.Path, ShaA, ShaA));
        Assert.Equal("NoExactSourceCandidate", Assert.Single(outcome.Attempts).MatchStatus);
    }

    [Fact]
    public void ForgedObservedEvidenceOrDifferentGraphRevisionIsRejected()
    {
        using var input = new TempTrx(Trx());
        var (graph, declarations) = Source();
        var run = TrxEvidenceReader.Import(input.Path, ShaA, ShaA);
        Assert.Throws<InvalidDataException>(() =>
            TrxSourceCandidateMatcher.Reconcile(graph, declarations,
                run with { MethodCallsObserved = true }));
        Assert.Throws<InvalidDataException>(() =>
            TrxSourceCandidateMatcher.Reconcile(graph, declarations,
                run with { GraphRevision = ShaB }));
        Assert.Throws<InvalidDataException>(() =>
            TrxSourceCandidateMatcher.Reconcile(graph, declarations,
                run with { Freshness = "Stale" }));
        Assert.Throws<InvalidDataException>(() =>
            TrxSourceCandidateMatcher.Reconcile(graph,
                declarations with { Associations =
                    [declarations.Associations[0] with { ExecutionEvidence = "Observed" }] }, run));
    }

    private static (GraphSnapshot, StaticTestAssociationReport) Source(bool duplicate = false)
    {
        const string second = "Tests.Two::M:Acme.Tests.CalculatorTests.ShouldWork";
        var nodes = new List<GraphNode>
        {
            new("solution:Acme", GraphNodeKind.Solution, "Acme", null, Evidence),
            new("project:Tests.One", GraphNodeKind.Project, "Tests.One", "solution:Acme", Evidence),
            new("project:Sut", GraphNodeKind.Project, "Sut", "solution:Acme", Evidence),
            new(SymbolId, GraphNodeKind.Method, "ShouldWork", "project:Tests.One", Evidence),
            new(TargetId, GraphNodeKind.Method, "Run", "project:Sut", Evidence)
        };
        var edges = new List<GraphEdge>
        {
            new("call-a", SymbolId, TargetId, "Calls", Evidence)
        };
        var tests = new List<StaticTestDeclaration>
        {
            new(SymbolId, "Tests.One", "xUnit", Evidence,
                "KnownAttributeNameUnverifiedByRunner",
                "TestMethodDeclarationNotParameterizedCase", "NotRun")
        };
        var links = new List<StaticMethodTestAssociation>
        {
            new(SymbolId, TargetId, "Tests.One", "xUnit",
                "DirectInvocation", "StaticSourceOnly", "Unobserved", "Unknown", [Evidence])
        };
        if (duplicate)
        {
            nodes.Add(new("project:Tests.Two", GraphNodeKind.Project, "Tests.Two",
                "solution:Acme", Evidence));
            nodes.Add(new(second, GraphNodeKind.Method, "ShouldWork", "project:Tests.Two", Evidence));
            tests.Add(new(second, "Tests.Two", "xUnit", Evidence,
                "KnownAttributeNameUnverifiedByRunner",
                "TestMethodDeclarationNotParameterizedCase", "NotRun"));
        }
        return (new("Acme", ShaA, nodes, edges, []),
            new("archpulse.method-test-associations/v1", "Acme", ShaA,
                "RoslynStaticDirectOnly", "NotRunnerDiscovered", "NoRunOrCoverageReceipts",
                tests, links, []));
    }

    private static string Result(string id, string executionId, string outcome) =>
        $$"""<UnitTestResult testId="{{id}}" executionId="{{executionId}}" outcome="{{outcome}}" testName="supersecret-creditcard"><Output><ErrorInfo><Message>sensitive-error</Message></ErrorInfo></Output></UnitTestResult>""";

    private static string Trx(string? results = null, bool withDefinition = true,
        string cls = "Acme.Tests.CalculatorTests", string method = "ShouldWork",
        bool twoDefinitions = false) =>
        $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun id="66666666-6666-4666-8666-666666666666" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions>
          {{(withDefinition ? $$"""<UnitTest id="{{TestIdA}}"><TestMethod className="{{cls}}" name="{{method}}" /></UnitTest>""" : "")}}
          {{(twoDefinitions ? $$"""<UnitTest id="{{TestIdB}}"><TestMethod className="{{cls}}" name="{{method}}" /></UnitTest>""" : "")}}
          </TestDefinitions>
          <Results>{{results ?? Result(TestIdA, ExecIdA, "Passed")}}</Results>
        </TestRun>
        """;

    private sealed class TempTrx : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "archpulse-trx-candidate-" + Guid.NewGuid().ToString("N") + ".trx");
        public TempTrx(string data) => File.WriteAllText(Path, data);
        public void Dispose() => File.Delete(Path);
    }
}
