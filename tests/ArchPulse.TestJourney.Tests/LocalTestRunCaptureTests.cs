// SPDX-License-Identifier: Apache-2.0
using ArchPulse.TestJourney;
using Xunit;

namespace ArchPulse.TestJourney.Tests;

public sealed class LocalTestRunCaptureTests
{
    [Fact]
    public void PreflightResolvesOnlyConventionalPrebuiltTestAssembly()
    {
        using var f = new Files();
        var result=LocalTestRunCapture.ValidatePaths(f.Root,f.TestProject,f.Output);
        Assert.Equal(f.TestProject,result.ProjectRelative);
        Assert.Equal("tests/Acme.Tests/bin/Release/net10.0/Acme.Tests.dll",
            result.AssemblyRelative);
        Assert.StartsWith(f.Root,result.ProjectPath,StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../outside/Unsafe.csproj")]
    [InlineData("tests/../outside/Unsafe.csproj")]
    [InlineData("/tmp/Unsafe.csproj")]
    [InlineData("apps/test/Acme.Tests.csproj")]
    [InlineData("tests/Acme.Tests/Acme.Tests.sh")]
    [InlineData("tests/Acme.Tests/Acme.Tests.csproj --filter *")]
    [InlineData("tests\\\\Acme.Tests\\\\Acme.Tests.csproj")]
    public void RejectsPathTraversalAbsoluteNonTestPathsAndArguments(string project)
    {
        using var f = new Files();
        Assert.Throws<InvalidDataException>(() =>
            LocalTestRunCapture.ValidatePaths(f.Root,project,f.Output));
    }

    [Fact]
    public void OutputInsideRepositoryIsRejected()
    {
        using var f = new Files();
        var inside = Path.Combine(f.Root,"local-output");
        Directory.CreateDirectory(inside);
        Assert.Throws<InvalidDataException>(() =>
            LocalTestRunCapture.ValidatePaths(f.Root,f.TestProject,inside));
    }

    [Fact]
    public void OutputMustBeEmptyAndAlreadyExist()
    {
        using var f = new Files();
        File.WriteAllText(Path.Combine(f.Output,"existing.txt"),"unrelated");
        Assert.Throws<InvalidDataException>(() =>
            LocalTestRunCapture.ValidatePaths(f.Root,f.TestProject,f.Output));
        File.Delete(Path.Combine(f.Output,"existing.txt"));
        Assert.Throws<InvalidDataException>(() =>
            LocalTestRunCapture.ValidatePaths(f.Root,f.TestProject,
                Path.Combine(f.Output,"nonexistent")));
    }

    [Fact]
    public void TestBinaryMustBePresentAndBounded()
    {
        using var f = new Files();
        File.Delete(f.Assembly);
        Assert.Throws<InvalidDataException>(() =>
            LocalTestRunCapture.ValidatePaths(f.Root,f.TestProject,f.Output));
        File.WriteAllBytes(f.Assembly,[]);
        Assert.Throws<InvalidDataException>(() =>
            LocalTestRunCapture.ValidatePaths(f.Root,f.TestProject,f.Output));
    }

    [Fact]
    public async Task DeniesLocalExecutionWithoutExplicitAuthorization()
    {
        using var f = new Files();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LocalTestRunCapture.CaptureAsync(f.Root,f.TestProject,f.Output,false));
    }

    [Fact]
    public async Task MissingGitRepositoryFailsBeforeLaunchingTestAssembly()
    {
        using var f = new Files();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LocalTestRunCapture.CaptureAsync(f.Root,f.TestProject,f.Output,true));
        Assert.Empty(Directory.EnumerateFileSystemEntries(f.Output));
    }

    [Fact]
    public void RejectsSymlinkedTestAssembly()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f = new Files();
        File.Delete(f.Assembly);
        var external=Path.Combine(Path.GetDirectoryName(f.Root)!,"untrusted.dll");
        File.WriteAllBytes(external,[42]);
        File.CreateSymbolicLink(f.Assembly,external);
        Assert.Throws<InvalidDataException>(() =>
            LocalTestRunCapture.ValidatePaths(f.Root,f.TestProject,f.Output));
    }

    private sealed class Files : IDisposable
    {
        private readonly string _base = Path.Combine(Path.GetTempPath(),
            "archpulse-test-capture-negative-"+Guid.NewGuid().ToString("N"));
        public string Root => Path.Combine(_base,"source");
        public string Output => Path.Combine(_base,"private-output");
        public string TestProject => "tests/Acme.Tests/Acme.Tests.csproj";
        public string Assembly => Path.Combine(Root,"tests/Acme.Tests/bin/Release/net10.0/Acme.Tests.dll");
        public Files()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root,"ArchPulse.sln"),"trusted-solution-fixture");
            string proj=Path.Combine(Root,TestProject);
            Directory.CreateDirectory(Path.GetDirectoryName(proj)!);
            File.WriteAllText(proj,"<Project />");
            Directory.CreateDirectory(Path.GetDirectoryName(Assembly)!);
            File.WriteAllBytes(Assembly,[1,2,3,4]);
            Directory.CreateDirectory(Output);
        }
        public void Dispose()=>Directory.Delete(_base,recursive:true);
    }
}
