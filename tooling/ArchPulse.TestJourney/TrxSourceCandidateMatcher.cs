// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using ArchPulse.Graph;

namespace ArchPulse.TestJourney;

/// <summary>
/// Matches *runner metadata to a source declaration candidate*, not test-to-SUT execution.
/// The source and TRX are independent evidence; name equality is not proof of test-case
/// identity, revision authenticity, observed calls or target method coverage.
/// </summary>
public static partial class TrxSourceCandidateMatcher
{
    public const string Schema = "archpulse.test-journey.source-candidates/v1";
    private const int MaxCandidates = 20_000;

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*){1,31}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SafeTypeName();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SafeMethodName();

    public static TestCaseCandidateReport Reconcile(
        GraphSnapshot graph, StaticTestAssociationReport declarations, TestRunEvidence run)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(run);
        StaticTestAssociationStore.Validate(graph, declarations);

        if (run.Schema != TrxEvidenceReader.Schema || run.Attempts is null ||
            run.Attempts.Count > MaxCandidates || run.AttemptCount != run.Attempts.Count ||
            run.RevisionVerification != "CallerProvidedUnverified" ||
            run.Runner != "VSTestTRX" ||
            run.CaptureProfile != "ResultsOnly" ||
            run.TraceStatus != "UnobservableFromTrx" ||
            run.MethodCallsObserved || run.ValuesCaptured || run.PerTestCoverageProven ||
            run.TrxSha256 is null || run.TrxSha256.Length != 64 ||
            !run.TrxSha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            run.TestedCommit is not null && !IsSha(run.TestedCommit) ||
            run.GraphRevision is not null && !IsSha(run.GraphRevision) ||
            run.GraphRevision is not null && run.GraphRevision != graph.Revision ||
            run.Freshness != (run.TestedCommit is null || run.GraphRevision is null ? "Unknown" :
                run.TestedCommit == run.GraphRevision ? "SameDeclaredRevision" : "Stale"))
            throw new InvalidDataException("TRX evidence and source graph are incompatible.");

        // A SHA supplied by the caller does not authenticate the checkout, test binary
        // or the provenance of the TRX bytes. Even a matching declaration is NOT verified.
        string revisionStatus =
            run.TestedCommit is null || run.GraphRevision is null
                ? "MissingDeclaredRevision"
                : run.TestedCommit != graph.Revision
                    ? "MismatchedDeclaredRevision"
                    : !IsSha(graph.Revision)
                        ? "UnsupportedRevisionIdentity"
                        : "MatchingDeclaredRevisionUnverified";

        // Build an *exact* class+method index from Roslyn documentation symbol IDs.
        // No substring/suffix/case-fold matching, no overload selection by guess,
        // no inferred equivalence of nested generic type encodings or constructor names.
        var known = new Dictionary<(string Class, string Method), List<StaticTestDeclaration>>();
        foreach (var declaration in declarations.TestDeclarations)
        {
            var identity = ParseSourceMethod(declaration.TestMethodSymbolId);
            if (identity is null) continue;
            if (!known.TryGetValue(identity.Value, out var list))
                known[identity.Value] = list = [];
            list.Add(declaration);
        }

        var attempts = new List<TestCaseSourceCandidate>(run.Attempts.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attempt in run.Attempts)
        {
            if (attempt is null || !seenIds.Add(attempt.AttemptId) ||
                string.IsNullOrWhiteSpace(attempt.TestCaseId) ||
                attempt.TraceAvailability != "UnobservableFromTrx" ||
                attempt.SutAssociationEvidence != "NoSutAssociationEvidence" ||
                attempt.Outcome is not ("Passed" or "Failed" or "Skipped" or "Cancelled" or "Unknown"))
                throw new InvalidDataException("Invalid or duplicate runner test attempt.");
            bool hasName = attempt.TestClass is not null && attempt.TestMethod is not null &&
                           SafeTypeName().IsMatch(attempt.TestClass) &&
                           SafeMethodName().IsMatch(attempt.TestMethod);

            // Do not attach runner outcomes to a source method for unknown/stale
            // declared versions or when metadata is absent/unsupported.
            var found = hasName && known.TryGetValue((attempt.TestClass!, attempt.TestMethod!),
                out var candidates) ? candidates! : [];
            string status = revisionStatus != "MatchingDeclaredRevisionUnverified"
                ? "RevisionNotCorrelatable"
                : !hasName ? "MissingOrUnsupportedRunnerName"
                : found.Count == 0 ? "NoExactSourceCandidate"
                : found.Count > 1 ? "AmbiguousExactName"
                : "UniqueNameCandidateUnverified";
            var candidate = status == "UniqueNameCandidateUnverified" ? found[0] : null;
            attempts.Add(new TestCaseSourceCandidate(
                attempt.AttemptId, attempt.TestCaseId, attempt.Outcome,
                attempt.TestClass, attempt.TestMethod,
                status, candidate?.TestMethodSymbolId, candidate?.TestProject, candidate?.Evidence,
                status == "AmbiguousExactName" ? found.Count : candidate is null ? 0 : 1,
                "TrxNameEqualsRoslynDeclarationOnly", "RunnerAttemptOnly",
                "Unobserved", false));
        }

        return new TestCaseCandidateReport(Schema,
            graph.Project, graph.Revision,
            run.TrxSha256, run.RunId,
            "VSTestTRX", "RunnerNameCandidateOnly",
            run.TestedCommit, revisionStatus, "CallerProvidedUnverified",
            "NoTestToSutRuntimeProof",
            attempts.Count(a => a.MatchStatus == "UniqueNameCandidateUnverified"),
            attempts.Count(a => a.MatchStatus == "AmbiguousExactName"),
            attempts.Count(a => a.MatchStatus == "NoExactSourceCandidate"),
            attempts.Count, attempts,
            [
                "Exact class/method equality is only a candidate: runner TestCaseId is not a Roslyn SymbolId.",
                "Caller-declared identical Git revisions do not authenticate TRX or test binary provenance.",
                "Test-run outcomes apply only to the listed attempts, never to a SUT method or static call edge.",
                "Parameterized cases and retries remain separate attempts. No per-test coverage or call trace."
            ]);
    }

    private static bool IsSha(string id) => id.Length == 40 &&
        id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static (string Class, string Method)? ParseSourceMethod(string symbolId)
    {
        int marker = symbolId.IndexOf("::M:", StringComparison.Ordinal);
        if (marker <= 0) return null;
        string remaining = symbolId[(marker + 4)..];
        int args = remaining.IndexOf('(');
        if (args >= 0) remaining = remaining[..args];
        int last = remaining.LastIndexOf('.');
        if (last <= 0 || last == remaining.Length - 1) return null;
        string typeName = remaining[..last];
        string method = remaining[(last + 1)..];
        if (!SafeTypeName().IsMatch(typeName) || !SafeMethodName().IsMatch(method))
            return null;
        return (typeName, method);
    }
}

public sealed record TestCaseSourceCandidate(
    string AttemptId, string TestCaseId, string RunnerOutcome,
    string? RunnerClass, string? RunnerMethod,
    string MatchStatus, string? CandidateTestMethodSymbolId,
    string? CandidateTestProject, SourceEvidence? CandidateEvidence, int CandidateCount,
    string IdentityEvidence, string OutcomeScope,
    string SutExecutionEvidence, bool PerTestCoverageProven);

public sealed record TestCaseCandidateReport(
    string Schema, string GraphProject, string GraphRevision,
    string TrxSha256, string RunId,
    string Runner, string MatchingScope, string? TestedCommit,
    string RevisionAssessment, string RevisionEvidence,
    string SutOutcomeEvidence, int UniqueNameCandidateAttempts,
    int AmbiguousNameAttempts, int NoSourceCandidateAttempts,
    int TotalAttempts,
    IReadOnlyList<TestCaseSourceCandidate> Attempts,
    IReadOnlyList<string> Gaps);
