// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchPulse.Roslyn;

/// <summary>
/// T027 partial: deterministic, bounded Roslyn local value propagation for
/// explicitly attributed sources, sinks and sanitizer claims. No interprocedural
/// proof, no runtime evidence, no trusted sanitizer guarantee, no value capture.
/// Branches/opaque functions/unsupported expressions always weaken the result.
/// Only invoke on TRUSTED MSBuild projects, never user-supplied project scripts.
/// </summary>
public static class LocalDataJourneyAnalyzer
{
    private const string SourceAttribute = "ArchPulse.DataJourney.SensitiveSourceAttribute";
    private const string SinkAttribute = "ArchPulse.DataJourney.SensitiveSinkAttribute";
    private const string SanitizerAttribute = "ArchPulse.DataJourney.DeclaredSanitizerAttribute";
    private const int MaxStatements = 128;
    private const int MaxFindings = 3000;

    private readonly record struct Value(string? Origin, bool IsRaw, bool IsSanitized, bool IsUnknown)
    {
        public static Value Clean => new(null, false, false, false);
        public static Value Unknown => new(null, false, false, true);
        public static Value Raw(string origin) => new(origin, true, false, false);
        public static Value Sanitized(string? origin) => new(origin, false, true, false);
    }

    public static LocalDataJourneyReport Analyze(GraphSnapshot graph,
        IEnumerable<(string ProjectName, CSharpCompilation Compilation)> compilations,
        string solutionRoot)
    {
        GraphSnapshotStore.Validate(graph);
        ArgumentNullException.ThrowIfNull(compilations);
        var graphMethods = graph.Nodes.Where(n => n.Kind == GraphNodeKind.Method)
            .Select(n => n.SymbolId).ToHashSet(StringComparer.Ordinal);
        var findings = new List<LocalDataFlowFinding>();
        var diagnostics = new List<string>();
        solutionRoot = Path.GetFullPath(solutionRoot);
        foreach (var (project, compilation) in compilations
            .OrderBy(x => x.ProjectName, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(project) ||
                !graph.Nodes.Any(n => n.SymbolId == "project:" + project))
                throw new InvalidDataException("DataJourney project compilation differs from source graph.");
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var syntax in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(syntax) is not IMethodSymbol method)
                        continue;
                    string id = project + "::" + (method.GetDocumentationCommentId() ??
                        method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    if (!graphMethods.Contains(id)) continue;
                    if (syntax.Body is null || syntax.Body.Statements.Count > MaxStatements)
                    {
                        if (syntax.Body is not null && diagnostics.Count < 500)
                            diagnostics.Add("Method exceeds straight-line analysis bound: " + Short(id));
                        continue; // Unsupported: never create a safe classification.
                    }

                    var locals = new Dictionary<ISymbol, Value>(SymbolEqualityComparer.Default);
                    foreach (var parameter in method.Parameters)
                        locals[parameter] = Has(parameter, SourceAttribute)
                            ? Value.Raw(id + "::parameter:" + parameter.Name)
                            : Value.Unknown;

                    // Do not fabricate 'unknown sensitive return' reports for
                    // unrelated code lacking any explicitly declared source.
                    // A sink call with unmarked inputs still yields Unknown.
                    bool hasSourceCandidate = method.Parameters.Any(parameter =>
                        Has(parameter, SourceAttribute)) ||
                        syntax.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                            .Any(call => model.GetSymbolInfo(call).Symbol is IMethodSymbol target &&
                                         Has(target, SourceAttribute));
                    bool uncertain = false;
                    foreach (var statement in syntax.Body.Statements)
                    {
                        // A construct we cannot model makes subsequent analysis Unknown,
                        // not falsely safe. Nested sink calls still get unknown witnesses.
                        if (statement is not (LocalDeclarationStatementSyntax or
                            ExpressionStatementSyntax or ReturnStatementSyntax))
                        {
                            uncertain = true;
                            ScanSinks(statement, unknownContext: true);
                            continue;
                        }

                        ScanSinks(statement, unknownContext: uncertain);
                        switch (statement)
                        {
                            case LocalDeclarationStatementSyntax declaration:
                                foreach (var variable in declaration.Declaration.Variables)
                                {
                                    if (model.GetDeclaredSymbol(variable) is not ILocalSymbol symbol)
                                        continue;
                                    locals[symbol] = variable.Initializer is null
                                        ? Value.Unknown
                                        : Evaluate(variable.Initializer.Value);
                                }
                                break;
                            case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                                when assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression):
                                var target = model.GetSymbolInfo(assignment.Left).Symbol;
                                if (target is not null &&
                                    (target is ILocalSymbol || target is IParameterSymbol))
                                    locals[target] = Evaluate(assignment.Right);
                                else uncertain = true;
                                break;
                            case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax }:
                                break;
                            case ReturnStatementSyntax { Expression: not null } returned:
                                var value = uncertain ? Value.Unknown : Evaluate(returned.Expression!);
                                if (value.IsRaw || value.IsSanitized ||
                                    value.IsUnknown && hasSourceCandidate)
                                    Emit(id, id + "::return", LocalFlowDestination.MethodReturn,
                                        value.IsUnknown || value.IsSanitized
                                            ? LocalFlowOutcome.Unknown
                                            : LocalFlowOutcome.StaticReturnPropagation,
                                        value.Origin, returned);
                                break;
                            default:
                                uncertain = true;
                                break;
                        }
                    }

                    void ScanSinks(StatementSyntax statement, bool unknownContext)
                    {
                        foreach (var call in statement.DescendantNodesAndSelf()
                                     .OfType<InvocationExpressionSyntax>())
                        {
                            if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol target ||
                                !Has(target, SinkAttribute)) continue;
                            var values = call.ArgumentList.Arguments.Select(a => Evaluate(a.Expression)).ToArray();
                            var outcome = unknownContext || values.Any(v => v.IsUnknown)
                                ? LocalFlowOutcome.Unknown
                                : values.Any(v => v.IsRaw)
                                    ? LocalFlowOutcome.StaticPossibleRisk
                                    : values.Any(v => v.IsSanitized)
                                        ? LocalFlowOutcome.DeclaredSanitizerNeedsReview
                                        : LocalFlowOutcome.NoCandidateInSubset;
                            var first = values.FirstOrDefault(v => v.IsRaw || v.IsSanitized);
                            Emit(id, "annotated-sink:" + (target.GetDocumentationCommentId()
                                 ?? target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                                LocalFlowDestination.DeclaredSink, outcome, first.Origin, call.Expression);
                        }
                    }

                    Value Evaluate(ExpressionSyntax expr)
                    {
                        switch (expr)
                        {
                            case LiteralExpressionSyntax:
                                return Value.Clean;
                            case ParenthesizedExpressionSyntax paren:
                                return Evaluate(paren.Expression);
                            case IdentifierNameSyntax identifier:
                                var symbol = model.GetSymbolInfo(identifier).Symbol;
                                return symbol is not null && locals.TryGetValue(symbol, out var value)
                                    ? value : Value.Unknown;
                            case InvocationExpressionSyntax invoke:
                                if (model.GetSymbolInfo(invoke).Symbol is not IMethodSymbol callee)
                                    return Value.Unknown;
                                if (Has(callee, SourceAttribute))
                                    return Value.Raw("annotated-source:" +
                                        (callee.GetDocumentationCommentId() ?? callee.Name));
                                if (Has(callee, SanitizerAttribute))
                                {
                                    var inputs = invoke.ArgumentList.Arguments
                                        .Select(a => Evaluate(a.Expression)).ToArray();
                                    if (inputs.Any(v => v.IsUnknown))
                                        return Value.Unknown;
                                    var relevant = inputs.FirstOrDefault(v => v.IsRaw || v.IsSanitized);
                                    return relevant.IsRaw || relevant.IsSanitized
                                        ? Value.Sanitized(relevant.Origin) : Value.Clean;
                                }
                                // An opaque call may transform/copy its inputs or synthesize
                                // sensitive values. Never prove 'safe' by dropping arguments.
                                return Value.Unknown;
                            default:
                                return Value.Unknown;
                        }
                    }
                }
            }
        }
        var report = new LocalDataJourneyReport(LocalDataJourneyContract.Schema,
            graph.Project, graph.Revision, LocalDataJourneyContract.Scope,
            LocalDataJourneyContract.RuntimeStatus,
            findings.OrderBy(f => f.MethodSymbolId, StringComparer.Ordinal)
                .ThenBy(f => f.Location.RelativeFile, StringComparer.Ordinal)
                .ThenBy(f => f.Location.Line)
                .ThenBy(f => f.Location.Column).ToArray(),
            diagnostics.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        LocalDataJourneyContract.Validate(graph, report);
        return report;

        void Emit(string id, string dest, LocalFlowDestination kind,
            LocalFlowOutcome result, string? source, SyntaxNode node)
        {
            if (findings.Count >= MaxFindings)
                throw new InvalidDataException("DataJourney findings cap exceeded.");
            if (dest.Length > 800 || source is { Length: > 800 })
                throw new InvalidDataException("DataJourney symbol exceeds bounded identity.");
            var location = node.GetLocation().GetLineSpan();
            var relative = Path.IsPathRooted(location.Path)
                ? Path.GetRelativePath(solutionRoot, location.Path).Replace('\\', '/')
                : location.Path.Replace('\\', '/');
            findings.Add(new LocalDataFlowFinding(id, dest, kind, result, source,
                new SourceEvidence(graph.Revision, relative,
                    location.StartLinePosition.Line + 1,
                    location.StartLinePosition.Character + 1, EvidenceKind.Static)));
        }
    }

    private static bool Has(ISymbol symbol, string attribute) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attribute);

    private static string Short(string value) =>
        value.Length <= 160 ? value : value[..160];
}
