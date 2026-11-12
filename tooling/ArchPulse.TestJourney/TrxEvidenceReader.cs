// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ArchPulse.TestJourney;

/// <summary>Source-constrained VSTest TRX results importer. Does not run tests,
/// execute untrusted code, claim observed call edges or expose raw failure text.</summary>
public static partial class TrxEvidenceReader
{
    public const long MaxTrxBytes = 16 * 1024 * 1024;
    public const int MaxAttempts = 20_000;
    public const string Schema = "archpulse.test-journey.results-only/v1";

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.+]{0,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCodeIdentifier();

    public static TestRunEvidence Import(string trxFile, string? testedCommit = null,
        string? graphRevision = null)
    {
        if (testedCommit is not null && !IsGitSha(testedCommit))
            throw new ArgumentException("Tested commit must be a 40-character lowercase SHA.", nameof(testedCommit));
        if (graphRevision is not null && !IsGitSha(graphRevision))
            throw new ArgumentException("Graph revision must be a 40-character lowercase SHA.", nameof(graphRevision));

        var file = new FileInfo(Path.GetFullPath(trxFile));
        if (!file.Exists)
            throw new FileNotFoundException("TRX file does not exist.", file.FullName);
        if (file.Length <= 0 || file.Length > MaxTrxBytes)
            throw new InvalidDataException("TRX must be between 1 byte and 16 MiB.");

        byte[] raw = File.ReadAllBytes(file.FullName);
        if (raw.LongLength > MaxTrxBytes)
            throw new InvalidDataException("TRX exceeds bounded import.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(raw));
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaxTrxBytes, MaxCharactersFromEntities = 0,
            IgnoreComments = true
        };
        using var input = new MemoryStream(raw, writable: false);
        using var reader = XmlReader.Create(input, settings);
        var root = XDocument.Load(reader, LoadOptions.None).Root;
        if (root?.Name.LocalName != "TestRun")
            throw new InvalidDataException("Expected a VSTest TestRun root.");
        string? declaredId = ValidGuid((string?)root.Attribute("id"));
        string runId = declaredId ?? "derived-" + hash[..24];

        var definitions = new Dictionary<string, (string? Class, string? Method)>(
            StringComparer.OrdinalIgnoreCase);
        var testDefinitions = Child(root, "TestDefinitions");
        if (testDefinitions is not null)
        {
            foreach (var unit in testDefinitions.Elements().Where(x => x.Name.LocalName == "UnitTest"))
            {
                var id = ValidGuid((string?)unit.Attribute("id"));
                if (id is null) continue;
                var method = Child(unit, "TestMethod");
                if (!definitions.TryAdd(id, (SafeCodeName((string?)method?.Attribute("className")),
                                             SafeCodeName((string?)method?.Attribute("name")))))
                    throw new InvalidDataException("Duplicate UnitTest definition.");
            }
        }

        var results = Child(root, "Results");
        var values = results?.Elements().Where(x => x.Name.LocalName == "UnitTestResult").ToArray() ?? [];
        if (values.Length > MaxAttempts)
            throw new InvalidDataException("More than 20,000 TRX test attempts.");

        var attempts = new List<TestCaseAttempt>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var id = ValidGuid((string?)value.Attribute("testId")) ??
                "unresolved-" + Convert.ToHexStringLower(SHA256.HashData(
                    Encoding.UTF8.GetBytes((string?)value.Attribute("testId") ?? "")))[..20];
            var execution = ValidGuid((string?)value.Attribute("executionId"));
            definitions.TryGetValue(id, out var definition);
            var attemptId = runId + ":" + id + ":" + (execution ?? "no-execution-id") + ":" +
                            (i + 1).ToString(CultureInfo.InvariantCulture);
            attempts.Add(new TestCaseAttempt(attemptId, id, definition.Class, definition.Method,
                NormalizeOutcome((string?)value.Attribute("outcome")),
                ParseDuration((string?)value.Attribute("duration")),
                execution is null ? "OrdinalOnly" : "TrxExecutionId",
                "UnobservableFromTrx", "NoSutAssociationEvidence"));
        }

        var summary = new TestResultCounts(
            attempts.Count(x => x.Outcome == "Passed"),
            attempts.Count(x => x.Outcome == "Failed"),
            attempts.Count(x => x.Outcome == "Skipped"),
            attempts.Count(x => x.Outcome == "Cancelled"),
            attempts.Count(x => x.Outcome == "Unknown"));
        var freshness = testedCommit is null || graphRevision is null ? "Unknown" :
            testedCommit == graphRevision ? "SameDeclaredRevision" : "Stale";
        return new TestRunEvidence(Schema, hash, "VSTestTRX", runId,
            declaredId is null ? "ContentDerived" : "TrxDeclared",
            testedCommit, graphRevision, "CallerProvidedUnverified", freshness,
            "ResultsOnly", "UnobservableFromTrx", false, false, false,
            attempts.Count, attempts.Select(x => x.TestCaseId).Distinct(StringComparer.Ordinal).Count(),
            summary, attempts,
            ["TRX results contain no method call/return spans, argument values or per-test coverage.",
             "Test metadata identifies test methods, not invocation of a target implementation.",
             "Caller-provided revision labels have not been checked against Git."]);
    }

    private static XElement? Child(XElement element, string name) =>
        element.Elements().FirstOrDefault(x => x.Name.LocalName == name);
    private static bool IsGitSha(string value) => value.Length == 40 &&
        value.All(c => (c >= 'a' && c <= 'f') || (c >= '0' && c <= '9'));
    private static string? ValidGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed.ToString("D") : null;
    private static string? SafeCodeName(string? value) =>
        value is not null && SafeCodeIdentifier().IsMatch(value) ? value : null;
    private static string NormalizeOutcome(string? value) => value switch
    {
        "Passed" => "Passed",
        "Failed" or "Error" or "Timeout" => "Failed",
        "NotExecuted" or "NotRunnable" => "Skipped",
        "Aborted" => "Cancelled",
        _ => "Unknown"
    };
    private static double? ParseDuration(string? value)
    {
        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration)
            || duration < TimeSpan.Zero || duration > TimeSpan.FromDays(7))
            return null;
        return duration.TotalMilliseconds;
    }
}

public sealed record TestCaseAttempt(string AttemptId, string TestCaseId,
    string? TestClass, string? TestMethod, string Outcome,
    double? DurationMilliseconds, string AttemptIdentityEvidence,
    string TraceAvailability, string SutAssociationEvidence);
public sealed record TestResultCounts(int Passed, int Failed, int Skipped, int Cancelled, int Unknown);
public sealed record TestRunEvidence(string Schema, string TrxSha256, string Runner,
    string RunId, string RunIdEvidence, string? TestedCommit, string? GraphRevision,
    string RevisionVerification, string Freshness, string CaptureProfile, string TraceStatus,
    bool MethodCallsObserved, bool ValuesCaptured, bool PerTestCoverageProven,
    int AttemptCount, int DistinctTestIds, TestResultCounts Counts,
    IReadOnlyList<TestCaseAttempt> Attempts, IReadOnlyList<string> Gaps);
