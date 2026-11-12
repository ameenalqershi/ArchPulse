// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Graph;
using ArchPulse.Roslyn;
using Xunit;

namespace ArchPulse.Roslyn.Tests;

public class StaticGraphIndexerTests
{
    private static GraphSnapshot Index(string content, string revision = "abc123") =>
        StaticGraphIndexer.IndexSources("Invoicing",
            new Dictionary<string, string> { ["src/Invoice.cs"] = content }, revision);

    [Fact]
    public void DiscoversRealTypeAndMethodDeclarationsWithEvidence()
    {
        var snapshot = Index("namespace Billing { public class Invoice { public int Total() => 2; } }");
        Assert.Contains(snapshot.Nodes, n => n.Kind == GraphNodeKind.Namespace && n.Label == "Billing");
        Assert.Contains(snapshot.Nodes, n => n.Kind == GraphNodeKind.Class && n.Label == "Invoice");
        var method = Assert.Single(snapshot.Nodes, n => n.Kind == GraphNodeKind.Method);
        Assert.Equal(EvidenceKind.Static, method.Evidence.Kind);
        Assert.Equal("abc123", method.Evidence.Revision);
        Assert.Equal("src/Invoice.cs", method.Evidence.RelativeFile);
        Assert.True(method.Evidence.Line > 0);
    }

    [Fact]
    public void SemanticModelAssociatesActualInternalMethodCalls()
    {
        var snapshot = Index("""
            namespace Billing;
            public class InvoiceService
            {
                public int CreateInvoice() => SaveInvoice(11);
                private int SaveInvoice(int id) => id;
            }
            """);
        var caller = Assert.Single(snapshot.Nodes, n => n.Kind == GraphNodeKind.Method && n.Label.Contains("CreateInvoice"));
        var callee = Assert.Single(snapshot.Nodes, n => n.Kind == GraphNodeKind.Method && n.Label.Contains("SaveInvoice"));
        var edge = Assert.Single(snapshot.CallsFrom(caller.SymbolId));
        Assert.Equal(callee.SymbolId, edge.ToSymbolId);
        Assert.Equal(EvidenceKind.Static, edge.Evidence.Kind);
        Assert.Equal("src/Invoice.cs", edge.Evidence.RelativeFile);
    }

    [Fact]
    public void ResolvedOverloadsHaveDistinctIds()
    {
        var snapshot = Index("""
            public class Calculator
            {
                public int Add(int a) => a;
                public int Add(int a, int b) => a+b;
                public int Run() => Add(1, 2);
            }
            """);
        var overloads = snapshot.Nodes.Where(n => n.Kind == GraphNodeKind.Method && n.Label.Contains("Add(", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, overloads.Length);
        Assert.Equal(2, overloads.Select(x => x.SymbolId).Distinct().Count());
        var call = Assert.Single(snapshot.Edges);
        Assert.Contains("System.Int32,System.Int32", call.ToSymbolId, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceRevisionChangesEvidenceButNotDeclarationIdentity()
    {
        const string source = "public class A { public int Run() => 1; }";
        var v1 = Index(source, "rev-a");
        var v2 = Index("\n" + source, "rev-b");
        var m1 = Assert.Single(v1.Nodes, n => n.Kind == GraphNodeKind.Method);
        var m2 = Assert.Single(v2.Nodes, n => n.Kind == GraphNodeKind.Method);
        Assert.Equal(m1.SymbolId, m2.SymbolId);
        Assert.Equal(m1.Evidence.Line + 1, m2.Evidence.Line);
        Assert.NotEqual(m1.Evidence.Revision, m2.Evidence.Revision);
    }

    [Fact]
    public void DoesNotInventCallEdgesToExternalFrameworkMethods()
    {
        var snapshot = Index("public class A { public void Run() { System.Console.WriteLine(1); } }");
        Assert.Empty(snapshot.Edges);
    }

    [Fact]
    public void RejectsMissingRevisionInsteadOfInventingProvenance()
    {
        Assert.Throws<ArgumentException>(() => Index("public class A {}", ""));
    }
}
