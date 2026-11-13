// SPDX-License-Identifier: Apache-2.0
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;
using Xunit;

namespace ArchPulse.Tooling.Tests;

public sealed class SolutionMembershipValidationTests
{
    [Fact]
    public void CanonicalSolutionContainsOnlyResolvableCsharpProjectPaths()
    {
        string root = RepositoryRoot.Find(AppContext.BaseDirectory);
        Assert.Empty(SolutionMembershipChecker.Validate(root));
    }

    [Fact]
    public void AcceptsRootSolutionWithProjectsAndVirtualFolders()
    {
        using var f = new Fixture();
        f.Project("src/Core/Core.csproj");
        f.Project("test/Core.Tests/Core.Tests.csproj");
        f.Solution(Folder("src", "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Entry("Core", "src\\Core\\Core.csproj", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            Entry("Core.Tests", "test/Core.Tests/Core.Tests.csproj", "cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
        Assert.Empty(SolutionMembershipChecker.Validate(f.Root));
    }

    [Fact]
    public void RejectsMissingReferencedProject()
    {
        using var f = new Fixture();
        f.Solution(Entry("Missing", "src/Missing.csproj", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
        Assert.Contains(SolutionMembershipChecker.Validate(f.Root),
            x => x.Contains("Missing solution C# project: src/Missing.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDuplicateGuidAndNormalizedProjectPath()
    {
        using var f = new Fixture();
        f.Project("src/Core.csproj");
        f.Solution(Entry("One", "src/Core.csproj", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            Entry("Two", "src/./Core.csproj", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
        var result = SolutionMembershipChecker.Validate(f.Root);
        Assert.Contains(result, x => x.Contains("Duplicate solution project GUID", StringComparison.Ordinal));
        Assert.Contains(result, x => x.Contains("Duplicate solution project path: src/Core.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsParentTraversalInsteadOfReadingOutsideTheRoot()
    {
        using var f = new Fixture();
        f.Solution(Entry("Escape", "../Outside.csproj", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
        Assert.Contains(SolutionMembershipChecker.Validate(f.Root),
            x => x.Contains("escapes repository root", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsAbsoluteAndDynamicProjectPaths()
    {
        using var f = new Fixture();
        f.Solution(Entry("Absolute", "/tmp/Candidate.csproj", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            Entry("Dynamic", "$(Root)/Candidate.csproj", "cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
        Assert.Equal(2, SolutionMembershipChecker.Validate(f.Root).Count(
            x => x.Contains("Unsupported C# solution project path", StringComparison.Ordinal)));
    }

    [Fact]
    public void RejectsMalformedCsharpProjectEntries()
    {
        using var f = new Fixture();
        f.Solution("Project(malformed)");
        Assert.Contains(SolutionMembershipChecker.Validate(f.Root),
            x => x.Contains("Malformed solution project entry", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsSymlinkedProjectEntries()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        string external = Path.Combine(Path.GetTempPath(), "archpulse-sln-external-" +
            Guid.NewGuid().ToString("N") + ".csproj");
        try
        {
            File.WriteAllText(external, "<Project/>");
            string link = Path.Combine(f.Root, "src", "Linked.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(link)!);
            File.CreateSymbolicLink(link, external);
            f.Solution(Entry("Linked", "src/Linked.csproj", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
            Assert.Contains(SolutionMembershipChecker.Validate(f.Root),
                x => x.Contains("Symlinked solution project is not allowed", StringComparison.Ordinal));
        }
        finally { File.Delete(external); }
    }

    [Fact]
    public void RejectsProjectInSymlinkedDirectory()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        string external = Path.Combine(Path.GetTempPath(), "archpulse-sln-tree-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(external);
        try
        {
            File.WriteAllText(Path.Combine(external, "Linked.csproj"), "<Project/>");
            Directory.CreateSymbolicLink(Path.Combine(f.Root, "linked"), external);
            f.Solution(Entry("Linked", "linked/Linked.csproj",
                "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
            Assert.Contains(SolutionMembershipChecker.Validate(f.Root),
                x => x.Contains("Symlinked solution project is not allowed", StringComparison.Ordinal));
        }
        finally { Directory.Delete(external, recursive: true); }
    }

    private static string Entry(string name, string project, string id) =>
        "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"" + name +
        "\", \"" + project + "\", \"{" + id + "}\"";

    private static string Folder(string name, string id) =>
        "Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"" + name +
        "\", \"" + name + "\", \"{" + id + "}\"";

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "archpulse-solution-" +
            Guid.NewGuid().ToString("N"));

        public Fixture() => Directory.CreateDirectory(Root);

        public void Project(string path)
        {
            string file = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "<Project/>");
        }

        public void Solution(params string[] entries) =>
            File.WriteAllText(Path.Combine(Root, "ArchPulse.sln"),
                "Microsoft Visual Studio Solution File, Format Version 12.00\n" +
                string.Join("\n", entries) + "\n");

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
