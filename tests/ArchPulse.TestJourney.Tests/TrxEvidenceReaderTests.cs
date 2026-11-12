// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Xml;
using ArchPulse.TestJourney;
using Xunit;

namespace ArchPulse.TestJourney.Tests;

public sealed class TrxEvidenceReaderTests
{
    private const string TestId = "11111111-1111-4111-8111-111111111111";
    private const string RunId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string ExecId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
    private const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static string Result(string outcome = "Passed", string testId = TestId,
        string executionId = ExecId, string duration = "00:00:00.045") =>
        $"""<UnitTestResult testId="{testId}" executionId="{executionId}" outcome="{outcome}" duration="{duration}" testName="LeakTest(card-number-secret)"><Output><StdOut>do-not-export-stdout</StdOut><ErrorInfo><Message>do-not-export-assertion</Message></ErrorInfo></Output></UnitTestResult>""";

    private static string Trx(string? results = null, bool includeDefinition = true) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun id="{RunId}" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions>
            {(includeDefinition ? $"""<UnitTest id="{TestId}"><TestMethod className="Billing.Tests.InvoiceTests" name="CreateInvoiceAsync" /></UnitTest>""" : "")}
          </TestDefinitions>
          <Results>{results ?? Result()}</Results>
        </TestRun>
        """;

    [Fact]
    public void RunnerOutcomeIsNotAnObservedSutInvocation()
    {
        using var f = new TempTrx(Trx());
        var report = TrxEvidenceReader.Import(f.Path);
        Assert.Equal(TrxEvidenceReader.Schema, report.Schema);
        Assert.Equal(RunId, report.RunId);
        Assert.Equal("TrxDeclared", report.RunIdEvidence);
        Assert.Equal("VSTestTRX", report.Runner);
        Assert.Equal(1, report.AttemptCount);
        Assert.Equal(1, report.DistinctTestIds);
        Assert.Equal(1, report.Counts.Passed);
        var attempt = Assert.Single(report.Attempts);
        Assert.Equal(TestId, attempt.TestCaseId);
        Assert.Equal("Billing.Tests.InvoiceTests", attempt.TestClass);
        Assert.Equal("CreateInvoiceAsync", attempt.TestMethod);
        Assert.Equal(45d, attempt.DurationMilliseconds);
        Assert.Equal("TrxExecutionId", attempt.AttemptIdentityEvidence);
        Assert.Equal("NoSutAssociationEvidence", attempt.SutAssociationEvidence);
        Assert.Equal("UnobservableFromTrx", attempt.TraceAvailability);
        Assert.Equal("Unknown", report.Freshness);
        Assert.Equal("CallerProvidedUnverified", report.RevisionVerification);
        Assert.False(report.MethodCallsObserved);
        Assert.False(report.ValuesCaptured);
        Assert.False(report.PerTestCoverageProven);
    }

    [Fact]
    public void FailureAndSensitiveLogsAreExcludedFromExport()
    {
        using var f = new TempTrx(Trx(Result("Failed")));
        var report = TrxEvidenceReader.Import(f.Path);
        Assert.Equal(1, report.Counts.Failed);
        var json = JsonSerializer.Serialize(report);
        Assert.DoesNotContain("do-not-export-stdout", json, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-export-assertion", json, StringComparison.Ordinal);
        Assert.DoesNotContain("card-number-secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RetriedTestRetainsTwoDistinctAttempts()
    {
        using var f = new TempTrx(Trx(Result() + Result("Failed")));
        var report = TrxEvidenceReader.Import(f.Path);
        Assert.Equal(2, report.AttemptCount);
        Assert.Equal(1, report.DistinctTestIds);
        Assert.Equal(1, report.Counts.Passed);
        Assert.Equal(1, report.Counts.Failed);
        Assert.NotEqual(report.Attempts[0].AttemptId, report.Attempts[1].AttemptId);
    }

    [Theory]
    [InlineData("NotExecuted", "Skipped")]
    [InlineData("NotRunnable", "Skipped")]
    [InlineData("Aborted", "Cancelled")]
    [InlineData("Timeout", "Failed")]
    [InlineData("Error", "Failed")]
    [InlineData("InProgress", "Unknown")]
    [InlineData("Passed", "Passed")]
    public void PreservesDistinctRunnerOutcomes(string original, string expected)
    {
        using var f = new TempTrx(Trx(Result(original)));
        Assert.Equal(expected, Assert.Single(TrxEvidenceReader.Import(f.Path).Attempts).Outcome);
    }

    [Fact]
    public void NoTestMethodDefinitionMeansNoSourceOrSutClaim()
    {
        using var f = new TempTrx(Trx(includeDefinition: false));
        var result = TrxEvidenceReader.Import(f.Path);
        Assert.Null(Assert.Single(result.Attempts).TestClass);
        Assert.Null(result.Attempts[0].TestMethod);
        Assert.Equal("NoSutAssociationEvidence", result.Attempts[0].SutAssociationEvidence);
    }

    [Fact]
    public void EmptyResultsNeverInventPassOrSpans()
    {
        using var f = new TempTrx(Trx(""));
        var result = TrxEvidenceReader.Import(f.Path);
        Assert.Equal(0, result.AttemptCount);
        Assert.Equal(0, result.Counts.Passed);
        Assert.Equal("UnobservableFromTrx", result.TraceStatus);
    }

    [Fact]
    public void RevisionMismatchIsStaleButTestOutcomeUnchanged()
    {
        using var f = new TempTrx(Trx());
        var result = TrxEvidenceReader.Import(f.Path, ShaA, ShaB);
        Assert.Equal("Stale", result.Freshness);
        Assert.Equal("Passed", result.Attempts[0].Outcome);
        Assert.Equal("CallerProvidedUnverified", result.RevisionVerification);
        Assert.False(result.MethodCallsObserved);
    }

    [Fact]
    public void MatchingDeclaredShaIsNotIndependentEvidence()
    {
        using var f = new TempTrx(Trx());
        var result = TrxEvidenceReader.Import(f.Path, ShaA, ShaA);
        Assert.Equal("SameDeclaredRevision", result.Freshness);
        Assert.Equal("CallerProvidedUnverified", result.RevisionVerification);
    }

    [Fact]
    public void RejectsInvalidShaAndExternalXmlEntities()
    {
        using var f = new TempTrx(Trx());
        Assert.Throws<ArgumentException>(() => TrxEvidenceReader.Import(f.Path, "main"));
        Assert.Throws<ArgumentException>(() => TrxEvidenceReader.Import(f.Path, ShaA.ToUpperInvariant()));
        using var evil = new TempTrx("<!DOCTYPE TestRun [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><TestRun>&e;</TestRun>");
        Assert.Throws<XmlException>(() => TrxEvidenceReader.Import(evil.Path));
    }

    [Fact]
    public void RejectsWrongRootAndDuplicateDefinitions()
    {
        using var invalid = new TempTrx("<NotTRX />");
        Assert.Throws<InvalidDataException>(() => TrxEvidenceReader.Import(invalid.Path));
        var duplicate = Trx().Replace("</TestDefinitions>",
            $"""<UnitTest id="{TestId}"><TestMethod name="Other" className="OtherClass"/></UnitTest></TestDefinitions>""",
            StringComparison.Ordinal);
        using var duplicateFile = new TempTrx(duplicate);
        Assert.Throws<InvalidDataException>(() => TrxEvidenceReader.Import(duplicateFile.Path));
    }

    [Fact]
    public void InvalidDurationIsUnknownAndDigestIsContentSensitive()
    {
        using var first = new TempTrx(Trx(Result(duration: "invalid")));
        using var second = new TempTrx(Trx(Result("Failed")));
        var a = TrxEvidenceReader.Import(first.Path);
        var b = TrxEvidenceReader.Import(first.Path);
        var c = TrxEvidenceReader.Import(second.Path);
        Assert.Null(a.Attempts[0].DurationMilliseconds);
        Assert.Equal(a.TrxSha256, b.TrxSha256);
        Assert.NotEqual(a.TrxSha256, c.TrxSha256);
        Assert.Equal(64, a.TrxSha256.Length);
    }

    [Fact]
    public void LargeTrxIsRejectedBeforeParsing()
    {
        using var f = new TempTrx(new string('A', (int)TrxEvidenceReader.MaxTrxBytes + 1));
        Assert.Throws<InvalidDataException>(() => TrxEvidenceReader.Import(f.Path));
    }

    private sealed class TempTrx : IDisposable
    {
        public TempTrx(string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "archpulse-trx-" + Guid.NewGuid().ToString("N") + ".trx");
            File.WriteAllText(Path, content);
        }
        public string Path { get; }
        public void Dispose() => File.Delete(Path);
    }
}
