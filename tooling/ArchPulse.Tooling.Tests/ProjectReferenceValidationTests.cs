// SPDX-License-Identifier: Apache-2.0
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;
using Xunit;

namespace ArchPulse.Tooling.Tests;

public sealed class ProjectReferenceValidationTests
{
    [Fact]
    public void RepositoryHasNoDanglingOrCircularLiteralProjectReferences()
    {
        string root = RepositoryRoot.Find(AppContext.BaseDirectory);
        Assert.Empty(ProjectReferenceChecker.Validate(root));
    }

    [Fact]
    public void AcceptsAnAcyclicCrossDirectoryProjectGraph()
    {
        using var fixture = new ProjectFixture();
        fixture.Project("Core/Core.csproj");
        fixture.Project("Service/Service.csproj", "../Core/Core.csproj");
        fixture.Project("App/App.csproj", "..\\Service\\Service.csproj", "../Core/Core.csproj");

        Assert.Empty(ProjectReferenceChecker.Validate(fixture.Root));
    }

    [Fact]
    public void RejectsDanglingReferenceWithRelativePathsOnly()
    {
        using var fixture = new ProjectFixture();
        fixture.Project("App/App.csproj", "../Missing/Missing.csproj");

        var errors = ProjectReferenceChecker.Validate(fixture.Root);
        Assert.Contains(errors, x => x.Contains("Missing local project reference", StringComparison.Ordinal) &&
            x.Contains("App/App.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, x => x.Contains(fixture.Root, StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsCircularAndSelfReferences()
    {
        using var fixture = new ProjectFixture();
        fixture.Project("A/A.csproj", "../B/B.csproj");
        fixture.Project("B/B.csproj", "../A/A.csproj");
        fixture.Project("Self/Self.csproj", "Self.csproj");

        var errors = ProjectReferenceChecker.Validate(fixture.Root);
        Assert.Contains(errors, x => x.Contains("A/A.csproj -> B/B.csproj -> A/A.csproj", StringComparison.Ordinal));
        Assert.Contains(errors, x => x.Contains("Self/Self.csproj -> Self/Self.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsReferencesOutsideTheTrustedRepository()
    {
        using var fixture = new ProjectFixture();
        fixture.Project("App/App.csproj", "../../outside.csproj");

        Assert.Contains(ProjectReferenceChecker.Validate(fixture.Root),
            x => x.Contains("escapes repository root", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnevaluatedMsbuildExpressionsRatherThanGuessing()
    {
        using var fixture = new ProjectFixture();
        fixture.Project("App/App.csproj", "$(SomeRoot)/Shared.csproj");
        Assert.Contains(ProjectReferenceChecker.Validate(fixture.Root),
            x => x.Contains("Unresolved project reference", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsProjectXmlWithDtd()
    {
        using var fixture = new ProjectFixture();
        fixture.RawProject("App/App.csproj", "<!DOCTYPE Project [<!ENTITY x 'unsafe'>]>" +
            "<Project><ItemGroup><ProjectReference Include='&x;'/></ItemGroup></Project>");

        Assert.Contains(ProjectReferenceChecker.Validate(fixture.Root),
            x => x.Contains("invalid or inaccessible project XML", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesSymlinkedProjectFileInsteadOfReadingOutsideTheRepository()
    {
        if (OperatingSystem.IsWindows()) return; // Creation may require Windows developer privilege.
        using var fixture = new ProjectFixture();
        string external = Path.Combine(Path.GetTempPath(), "archpulse-external-" +
            Guid.NewGuid().ToString("N") + ".csproj");
        try
        {
            File.WriteAllText(external, "<Project/>");
            string local = Path.Combine(fixture.Root, "Link", "Link.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(local)!);
            File.CreateSymbolicLink(local, external);
            Assert.Contains(ProjectReferenceChecker.Validate(fixture.Root),
                error => error.Contains("Symlinked project file is not allowed", StringComparison.Ordinal));
        }
        finally { File.Delete(external); }
    }

    [Fact]
    public void IgnoresGeneratedProjectsUnderObj()
    {
        using var fixture = new ProjectFixture();
        fixture.Project("Core/Core.csproj");
        fixture.Project("Core/obj/Generated.csproj", "../missing.csproj");
        Assert.Empty(ProjectReferenceChecker.Validate(fixture.Root));
    }

    private sealed class ProjectFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archpulse-project-refs-" +
            Guid.NewGuid().ToString("N"));

        public void Project(string path, params string[] references)
        {
            var refs = string.Join("", references.Select(r =>
                "<ProjectReference Include='" + r.Replace("&", "&amp;").Replace("'", "&apos;") + "'/>"));
            RawProject(path, "<Project Sdk='Microsoft.NET.Sdk'><ItemGroup>" + refs +
                "</ItemGroup></Project>");
        }

        public void RawProject(string path, string xml)
        {
            string destination = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, xml);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
