// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ArchPulse.Roslyn;

/// <summary>Static string-flow witnesses through bounded, directly bound source calls.</summary>
internal sealed class CrossProjectDataJourneyAnalyzer
{
    private const int MaxDepth = 5;
    private const int MaxStatements = 128;
    private const int MaxHops = CrossProjectDataJourneyStore.MaxHops;
    private const int MaxStepsPerRoot = 2048;
    private const int MaxFindings = CrossProjectDataJourneyStore.MaxFindings;
    private const int MaxStepsPerReport = 100_000;

    private readonly GraphSnapshot _graph;
    private readonly string _root;
    private readonly CancellationToken _cancellation;
    private readonly Dictionary<(AssemblyIdentity Assembly, string DocId), Method> _methods = [];
    private readonly Dictionary<(string Caller, string Callee, SourceEvidence Site), GraphEdge> _calls;
    private readonly List<CrossProjectDataFlow> _findings = [];

    private sealed record Method(string Id, string Project, IMethodSymbol Symbol,
        SemanticModel Model, MethodDeclarationSyntax Syntax);

    private sealed record Flow(string MethodId, SourceEvidence Origin, DataJourneyHop[] Hops,
        bool CrossesProject);

    private bool _truncated;
    private int _remainingSteps;
    private int _remainingReportSteps = MaxStepsPerReport;

    private CrossProjectDataJourneyAnalyzer(GraphSnapshot graph, string root, CancellationToken cancellation)
    {
        _graph = graph;
        _root = root;
        _cancellation = cancellation;
        _calls = graph.Edges.Where(e => e.Relation == "Calls")
            .ToLookup(e => (e.FromSymbolId, e.ToSymbolId, e.Evidence))
            .ToDictionary(g => g.Key, g => g.First());
    }

    public static CrossProjectDataJourneyReport Analyze(GraphSnapshot graph,
        IEnumerable<(string Project, CSharpCompilation Compilation)> compilations,
        string root, CancellationToken cancellation)
    {
        var analyzer = new CrossProjectDataJourneyAnalyzer(graph, root, cancellation);
        return analyzer.Run(compilations);
    }

    private CrossProjectDataJourneyReport Run(
        IEnumerable<(string Project, CSharpCompilation Compilation)> compilations)
    {
        var methodIds = _graph.Nodes.Where(n => n.Kind == GraphNodeKind.Method)
            .Select(n => n.SymbolId).ToHashSet(StringComparer.Ordinal);

        foreach (var (project, compilation) in compilations)
        foreach (var tree in compilation.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal))
        {
            _cancellation.ThrowIfCancellationRequested();
            var model = compilation.GetSemanticModel(tree);
            foreach (var syntax in tree.GetRoot(_cancellation).DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(syntax, _cancellation) is not IMethodSymbol symbol ||
                    symbol.GetDocumentationCommentId() is not { } docId)
                    continue;
                string id = project + "::" + docId;
                if (methodIds.Contains(id) && Site(syntax) is not null)
                    _methods.TryAdd((symbol.ContainingAssembly.Identity, docId),
                        new(id, project, symbol, model, syntax));
            }
        }

        foreach (var method in _methods.Values.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            _cancellation.ThrowIfCancellationRequested();
            if (_findings.Count == MaxFindings || _remainingReportSteps <= 0)
            {
                _truncated = true;
                break;
            }

            var parameters = new Flow?[method.Symbol.Parameters.Length];
            foreach (var parameter in method.Symbol.Parameters)
                if (IsString(parameter.Type) && parameter.RefKind == RefKind.None &&
                    LocalDataJourneyAnalyzer.Has(parameter, LocalDataJourneyAnalyzer.SourceAttribute) &&
                    method.Syntax.ParameterList.Parameters.ElementAtOrDefault(parameter.Ordinal)
                        is { } parameterDeclaration && Site(parameterDeclaration) is { } origin)
                    parameters[parameter.Ordinal] = new(method.Id, origin, [], false);

            if (parameters.All(p => p is null) &&
                !method.Syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                    method.Model.GetSymbolInfo(call, _cancellation).Symbol is IMethodSymbol target &&
                    IsString(target.ReturnType) &&
                    LocalDataJourneyAnalyzer.Has(target, LocalDataJourneyAnalyzer.SourceAttribute)))
                continue;

            _remainingSteps = MaxStepsPerRoot;
            Interpret(method, parameters, []);
        }

        return new(CrossProjectDataJourneyStore.Schema, _graph.Project, _graph.Revision,
            EvidenceKind.Static, "Unknown", _truncated, _findings);
    }

    private Flow? Interpret(Method method, Flow?[] parameters, IReadOnlyList<string> stack)
    {
        _cancellation.ThrowIfCancellationRequested();
        var boundValues = new Dictionary<ISymbol, Flow?>(SymbolEqualityComparer.Default);
        foreach (var parameter in method.Symbol.Parameters)
            boundValues.Add(parameter, parameters[parameter.Ordinal]);

        if (method.Symbol.IsAsync || method.Syntax.DescendantNodes().Any(node =>
            node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or RefExpressionSyntax ||
            node is ArgumentSyntax argument && argument.RefKindKeyword.RawKind != 0))
        {
            RecordGaps(boundValues.Values, DataJourneyBoundary.UnsupportedSyntax, method.Syntax);
            return null;
        }

        bool unsupportedExpression = false;
        if (method.Syntax.ExpressionBody is { Expression: { } expression })
            return method.Symbol.ReturnsVoid ? Evaluate(expression) : EvaluateAssignment(expression, method.Symbol.ReturnType);
        if (method.Syntax.Body is not { Statements: { Count: <= MaxStatements } statements })
        {
            RecordGaps(boundValues.Values, DataJourneyBoundary.UnsupportedSyntax, method.Syntax);
            return null;
        }

        foreach (var statement in statements)
        {
            if (_remainingSteps <= 0 || _remainingReportSteps <= 0)
            {
                _truncated = true;
                RecordGaps(boundValues.Values, DataJourneyBoundary.AnalysisLimit, statement);
                return null;
            }

            switch (statement)
            {
                case LocalDeclarationStatementSyntax declaration when
                    declaration.UsingKeyword.RawKind == 0 && declaration.AwaitKeyword.RawKind == 0:
                    foreach (var variable in declaration.Declaration.Variables)
                    {
                        if (method.Model.GetDeclaredSymbol(variable, _cancellation) is ILocalSymbol local &&
                            local.RefKind == RefKind.None)
                            boundValues[local] = variable.Initializer is { Value: { } initializer }
                                ? EvaluateAssignment(initializer, local.Type) : null;
                    }
                    break;
                case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
                    Evaluate(invocation);
                    break;
                case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                    when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                        assignment.Left is IdentifierNameSyntax &&
                        method.Model.GetSymbolInfo(assignment.Left, _cancellation).Symbol is { } target &&
                        boundValues.ContainsKey(target):
                    boundValues[target] = EvaluateAssignment(assignment.Right,
                        target is ILocalSymbol assignedLocal ? assignedLocal.Type : ((IParameterSymbol)target).Type);
                    break;
                case ReturnStatementSyntax returned:
                    return returned.Expression is null ? null : EvaluateAssignment(returned.Expression, method.Symbol.ReturnType);
                default:
                    RecordGaps(boundValues.Values, DataJourneyBoundary.UnsupportedSyntax, statement);
                    return null;
            }
            if (unsupportedExpression) return null;
        }
        return null;

        Flow? EvaluateAssignment(ExpressionSyntax node, ITypeSymbol targetType)
        {
            var value = Evaluate(node);
            if (value is null || IsString(targetType) && method.Model.GetConversion(node, _cancellation).IsIdentity)
                return value;
            RecordGaps([value], DataJourneyBoundary.UnsupportedSyntax, node);
            return null;
        }

        Flow? Evaluate(ExpressionSyntax node)
        {
            if (unsupportedExpression) return null;
            if (_remainingSteps-- <= 0 || _remainingReportSteps-- <= 0)
            {
                _truncated = true;
                RecordGaps(boundValues.Values, DataJourneyBoundary.AnalysisLimit, node);
                return null;
            }

            switch (node)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    return Evaluate(parenthesized.Expression);
                case LiteralExpressionSyntax:
                    return null;
                case IdentifierNameSyntax identifier:
                    var symbol = method.Model.GetSymbolInfo(identifier, _cancellation).Symbol;
                    return symbol is not null ? boundValues.GetValueOrDefault(symbol) : null;
                case InvocationExpressionSyntax call:
                    return FollowCall(call);
                default:
                    unsupportedExpression = true;
                    RecordGaps(boundValues.Values, DataJourneyBoundary.UnsupportedSyntax, node);
                    return null;
            }
        }

        Flow? FollowCall(InvocationExpressionSyntax syntax)
        {
            if (syntax.ArgumentList.DescendantNodes().Any(node => node is AssignmentExpressionSyntax))
            {
                unsupportedExpression = true;
                RecordGaps(boundValues.Values, DataJourneyBoundary.UnsupportedSyntax, syntax);
                return null;
            }

            if (method.Model.GetOperation(syntax, _cancellation) is not IInvocationOperation operation)
            {
                RecordGaps(boundValues.Values, DataJourneyBoundary.OpaqueCall, syntax);
                return null;
            }

            var symbol = operation.TargetMethod;
            var args = new Flow?[symbol.Parameters.Length];
            foreach (var argument in operation.Arguments)
            {
                if (argument.IsImplicit || argument.Parameter is null ||
                    argument.Syntax is not ArgumentSyntax explicitArgument)
                    continue;
                var input = Evaluate(explicitArgument.Expression);
                if (unsupportedExpression) return null;
                if (argument.InConversion.IsIdentity && IsString(argument.Parameter.Type))
                    args[argument.Parameter.Ordinal] = input;
                else if (input is not null)
                    RecordGaps([input], DataJourneyBoundary.UnsupportedSyntax, explicitArgument);
            }

            if (!TryBindCall(method, syntax, operation, out var callee, out var edge))
            {
                // A receiver, params expansion or unresolved binding can hide an input.
                if (operation.Instance is not null || operation.Arguments.Any(a => a.IsImplicit))
                    RecordGaps(boundValues.Values.Concat(args), DataJourneyBoundary.OpaqueCall, syntax);
                else
                    RecordGaps(args, DataJourneyBoundary.OpaqueCall, syntax);
                return null;
            }

            bool crossesProject = method.Project != callee.Project;
            for (int i = 0; i < args.Length; i++)
                args[i] = AddHop(args[i], DataJourneyHopKind.Argument, edge, crossesProject);

            if (LocalDataJourneyAnalyzer.Has(callee.Symbol, LocalDataJourneyAnalyzer.SinkAttribute))
            {
                foreach (var input in args.OfType<Flow>().Where(f => f.CrossesProject).Distinct())
                    Record(input, DataJourneyBoundary.AnnotatedSink, edge.Evidence);
                return null;
            }
            if (LocalDataJourneyAnalyzer.Has(callee.Symbol, LocalDataJourneyAnalyzer.SanitizerAttribute))
            {
                RecordGaps(args, DataJourneyBoundary.DeclaredSanitizer, syntax);
                return null;
            }
            if (IsString(callee.Symbol.ReturnType) &&
                LocalDataJourneyAnalyzer.Has(callee.Symbol, LocalDataJourneyAnalyzer.SourceAttribute) &&
                Site(callee.Syntax) is { } origin)
                return AddHop(new(callee.Id, origin, [], false), DataJourneyHopKind.Return, edge, crossesProject);

            if (args.All(a => a is null))
                return null;
            if (stack.Count >= MaxDepth || callee.Id == method.Id || stack.Contains(callee.Id))
            {
                _truncated = true;
                RecordGaps(args, DataJourneyBoundary.AnalysisLimit, syntax);
                return null;
            }

            return AddHop(Interpret(callee, args, [.. stack, method.Id]),
                DataJourneyHopKind.Return, edge, crossesProject);
        }
    }

    private Flow? AddHop(Flow? flow, DataJourneyHopKind kind, GraphEdge edge, bool crossesProject)
    {
        if (flow is null) return null;
        if (flow.Hops.Length < MaxHops)
            return flow with
            {
                Hops = [.. flow.Hops, new(kind, edge)],
                CrossesProject = flow.CrossesProject || crossesProject
            };
        _truncated = true;
        Record(flow, DataJourneyBoundary.AnalysisLimit, edge.Evidence);
        return null;
    }

    private bool TryBindCall(Method caller, InvocationExpressionSyntax syntax,
        IInvocationOperation operation, out Method callee, out GraphEdge edge)
    {
        callee = null!;
        edge = null!;
        var symbol = operation.TargetMethod;
        if (!symbol.IsStatic || symbol.MethodKind != MethodKind.Ordinary || symbol.IsExtensionMethod ||
            symbol.IsAsync || symbol.IsExtern || symbol.IsGenericMethod || symbol.ReturnsByRef ||
            symbol.ReturnsByRefReadonly || symbol.Parameters.Any(p => p.RefKind != RefKind.None || p.IsParams) ||
            operation.Arguments.Any(a => a.IsImplicit || a.ArgumentKind != ArgumentKind.Explicit) ||
            (symbol.ContainingType.IsGenericType && !SymbolEqualityComparer.Default.Equals(
                symbol.ContainingType, symbol.ContainingType.OriginalDefinition)) ||
            symbol.OriginalDefinition.GetDocumentationCommentId() is not { } docId ||
            !_methods.TryGetValue((symbol.ContainingAssembly.Identity, docId), out var target) ||
            (target.Syntax.Body is null && target.Syntax.ExpressionBody is null) ||
            Site(syntax) is not { } site ||
            !_calls.TryGetValue((caller.Id, target.Id, site), out var found))
            return false;

        callee = target;
        edge = found;
        return true;
    }

    private void RecordGaps(IEnumerable<Flow?> values, DataJourneyBoundary boundary, SyntaxNode node)
    {
        if (Site(node) is not { } location) return;
        foreach (var flow in values.OfType<Flow>().Distinct())
            Record(flow, boundary, location);
    }

    private void Record(Flow flow, DataJourneyBoundary boundary, SourceEvidence destination)
    {
        if (_findings.Count == MaxFindings) { _truncated = true; return; }
        _findings.Add(new(flow.MethodId, flow.Origin, flow.Hops, boundary, destination));
    }

    private SourceEvidence? Site(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        if (string.IsNullOrEmpty(span.Path)) return null;
        var relative = (Path.IsPathRooted(span.Path)
            ? Path.GetRelativePath(_root, span.Path) : span.Path).Replace('\\', '/');
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p == "..") ||
            relative.Any(char.IsControl))
            return null;
        return new(_graph.Revision, relative, span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1, EvidenceKind.Static);
    }

    private static bool IsString(ITypeSymbol type) => type.SpecialType == SpecialType.System_String;
}
