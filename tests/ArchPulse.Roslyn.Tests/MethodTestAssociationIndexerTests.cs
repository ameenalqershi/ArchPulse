// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public sealed class MethodTestAssociationIndexerTests
{
    private const string Revision = "fixture-revision-1";
    private const string Sut = """
        namespace Demo;
        public sealed class Calculator
        {
            public int Add(int value) => value + 1;
            public int Hidden() => 0;
        }
        """;

    private const string Tests = """
        using Xunit;
        using Demo;
        public sealed class CalculatorTests
        {
            [Fact]
            public void Direct() => new Calculator().Add(1);

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void Parameterized(int value) => new Calculator().Add(value);

            [Fact]
            public void Deferred()
            {
                System.Func<int> callback = () => new Calculator().Hidden();
            }

            [Fact]
            public void LocalFunction()
            {
                int callback() => new Calculator().Hidden();
            }

            [Fact]
            public void Repeated()
            {
                var target = new Calculator();
                target.Add(1);
                target.Add(2);
            }

            public void NotATest() => new Calculator().Hidden();
        }
        """;

    [Fact]
    public void DirectAndParameterizedTestsMapToExactSutSymbolsOnly()
    {
        var (snapshot, compilation) = Build(Tests);
        var report = MethodTestAssociationIndexer.Project(snapshot, [("Tests", compilation)]);
        Assert.Equal(MethodTestAssociationIndexer.Schema, report.Schema);
        Assert.Equal(Revision, report.GraphRevision);
        Assert.Equal("RoslynStaticDirectOnly", report.MappingScope);
        Assert.Equal("NotRunnerDiscovered", report.RunnerDiscovery);
        Assert.Equal("NoRunOrCoverageReceipts", report.ResultEvidence);

        var add = Assert.Single(snapshot.Nodes, n => n.Kind == GraphNodeKind.Method
            && n.SymbolId.StartsWith("Sut::", StringComparison.Ordinal)
            && n.Label.Contains("Add(", StringComparison.Ordinal));
        var found = report.ForTarget(add.SymbolId);
        Assert.Equal(3, found.Count);
        Assert.All(found, mapping =>
        {
            Assert.Equal(add.SymbolId, mapping.TargetSymbolId);
            Assert.Equal("Tests", mapping.TestProject);
            Assert.Equal("xUnit", mapping.Framework);
            Assert.Equal("DirectInvocation", mapping.Relation);
            Assert.Equal("StaticSourceOnly", mapping.Verification);
            Assert.Equal("Unobserved", mapping.ExecutionEvidence);
            Assert.Equal("Unknown", mapping.LastResult);
            Assert.All(mapping.CallSites, evidence =>
            {
                Assert.Equal(EvidenceKind.Static, evidence.Kind);
                Assert.Equal(Revision, evidence.Revision);
                Assert.True(evidence.Line > 0);
            });
        });
        Assert.Equal(5, report.TestDeclarations.Count);
        Assert.All(report.TestDeclarations, d =>
        {
            Assert.Equal("KnownAttributeNameUnverifiedByRunner", d.DiscoveryEvidence);
            Assert.Equal("TestMethodDeclarationNotParameterizedCase", d.CaseGranularity);
            Assert.Equal("NotRun", d.LastResult);
        });
        Assert.Contains(report.TestDeclarations, d => d.TestMethodSymbolId.EndsWith(".Parameterized(System.Int32)",
            StringComparison.Ordinal));
    }

    [Fact]
    public void RepeatedCallsAreOneMappingWithSeparateStaticEvidenceSites()
    {
        var (snapshot, compilation) = Build(Tests);
        var result = MethodTestAssociationIndexer.Project(snapshot, [("Tests", compilation)]);
        var repeated = Assert.Single(result.Associations, a =>
            a.TestMethodSymbolId.EndsWith(".Repeated", StringComparison.Ordinal));
        Assert.Equal(2, repeated.CallSites.Count);
        Assert.NotEqual(repeated.CallSites[0].Line, repeated.CallSites[1].Line);
        Assert.Single(result.Associations, a =>
            a.TestMethodSymbolId.EndsWith(".Parameterized(System.Int32)", StringComparison.Ordinal));
    }

    [Fact]
    public void LambdaAndLocalFunctionCallsDoNotPretendToBeDirectTestCalls()
    {
        var (snapshot, compilation) = Build(Tests);
        var result = MethodTestAssociationIndexer.Project(snapshot, [("Tests", compilation)]);
        Assert.DoesNotContain(result.Associations, a =>
            a.TestMethodSymbolId.EndsWith(".Deferred", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Associations, a =>
            a.TestMethodSymbolId.EndsWith(".LocalFunction", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Associations, a =>
            a.TestMethodSymbolId.EndsWith(".NotATest", StringComparison.Ordinal));

        var hidden = Assert.Single(snapshot.Nodes, n => n.Kind == GraphNodeKind.Method
            && n.SymbolId.StartsWith("Sut::", StringComparison.Ordinal)
            && n.Label.Contains("Hidden(", StringComparison.Ordinal));
        Assert.Empty(result.ForTarget(hidden.SymbolId));
    }

    [Fact]
    public void UnrelatedAttributesAndCallsToOwnTestHelpersAreNotLinkedToSut()
    {
        const string source = """
            using Demo;
            public sealed class LooksLikeTestAttribute : System.Attribute {}
            public class Example
            {
                [LooksLikeTest]
                public void NamedLikeATest() { new Calculator().Add(1); }

                [Xunit.Fact]
                public void RealAnnotatedTest() { Helper(); }

                public void Helper() { new Calculator().Add(2); }
            }
            """;
        var (snapshot, compilation) = Build(source);
        var report = MethodTestAssociationIndexer.Project(snapshot, [("Tests", compilation)]);
        Assert.Single(report.TestDeclarations);
        Assert.Empty(report.Associations);
    }

    [Fact]
    public void NUnitAndMSTestAttributeNamesAreDetectedWithoutClaimingRunnerVerification()
    {
        const string source = """
            using Demo;
            namespace NUnit.Framework
            {
                public sealed class TestAttribute : System.Attribute { }
            }
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
            }
            public class OtherFrameworks
            {
                [NUnit.Framework.Test]
                public void NunitCase() { new Calculator().Add(1); }
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                public void MsTestCase() { new Calculator().Add(2); }
            }
            """;
        var (snapshot, compilation) = Build(source);
        var report = MethodTestAssociationIndexer.Project(snapshot, [("Tests", compilation)]);
        Assert.Equal(2, report.Associations.Count);
        Assert.Equal(["MSTest", "NUnit"],
            report.Associations.Select(a => a.Framework).Order(StringComparer.Ordinal).ToArray());
        Assert.All(report.TestDeclarations, d =>
            Assert.Equal("KnownAttributeNameUnverifiedByRunner", d.DiscoveryEvidence));
    }

    [Fact]
    public void RequiresSourceBackedProjectNodesAndValidGraph()
    {
        var (snapshot, compilation) = Build(Tests);
        var noProject = snapshot with
        {
            Nodes = snapshot.Nodes.Where(n => n.SymbolId != "project:Tests").ToArray()
        };
        Assert.Throws<InvalidDataException>(() =>
            MethodTestAssociationIndexer.Project(noProject, [("Tests", compilation)]));
        Assert.Throws<InvalidDataException>(() =>
            MethodTestAssociationIndexer.Project(snapshot with { Revision = "different" },
                [("Tests", compilation)]));
    }

    [Fact]
    public async Task RealGoldenSolutionDoesNotInventTestsWhenNoTestProjectExists()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ArchPulse.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "examples", "billing-multi-project", "Billing.sln");
        var result = await new SolutionGraphIndexer().AnalyzeSolutionAsync(path, Revision);
        Assert.Equal(11, result.Graph.Nodes.Count);
        Assert.Equal(3, result.Graph.Edges.Count);
        Assert.Empty(result.TestAssociations.TestDeclarations);
        Assert.Empty(result.TestAssociations.Associations);
    }

    private static (GraphSnapshot Snapshot, CSharpCompilation TestCompilation) Build(
        string testSource)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => (MetadataReference)MetadataReference.CreateFromFile(x)).ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location));

        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var sut = CSharpCompilation.Create("SutAssembly",
            [CSharpSyntaxTree.ParseText(Sut, path: "Sut/Calculator.cs")], references, options);
        var tests = CSharpCompilation.Create("TestsAssembly",
            [CSharpSyntaxTree.ParseText(testSource, path: "Tests/CalculatorTests.cs")],
            references.Append(sut.ToMetadataReference()), options);

        Assert.DoesNotContain(tests.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SutAssembly"] = "Sut",
            ["TestsAssembly"] = "Tests"
        };
        var first = StaticGraphIndexer.IndexCompilation("Sut", sut, Revision, names);
        var second = StaticGraphIndexer.IndexCompilation("Tests", tests, Revision, names);
        var graph = new GraphSnapshot("Sample", Revision,
            first.Nodes.Concat(second.Nodes).ToArray(),
            first.Edges.Concat(second.Edges).ToArray(),
            first.Diagnostics.Concat(second.Diagnostics).ToArray());
        GraphSnapshotStore.Validate(graph);
        return (graph, tests);
    }
}
