// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ArchPulse.TestJourney;

/// <summary>
/// Explicit user-authorized trusted-checkout local VSTest invocation with bounded,
/// independently hashed local bytes. NOT an isolation/sandbox API, runner-to-source
/// build attestation, case identity binding, SUT trace or remote execution endpoint.
/// </summary>
public static partial class LocalTestRunCapture
{
    public const string Schema = "archpulse.test-journey.local-run-receipt/v1";
    public const string TrxFile = "archpulse-local-captured.trx";
    private const long MaxAssemblyBytes = 128 * 1024 * 1024;

    [GeneratedRegex("^[a-f0-9]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex GitSha();
    [GeneratedRegex(@"^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PathSegment();

    /// <summary>Validate paths before executing any external process.</summary>
    public static CapturePaths ValidatePaths(string repoRoot,
        string testProjectRelative, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) ||
            string.IsNullOrWhiteSpace(outputDirectory) ||
            string.IsNullOrWhiteSpace(testProjectRelative))
            throw new ArgumentException("Root, test project and private output directory are required.");

        string root = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "ArchPulse.sln")))
            throw new InvalidDataException("Trusted repository root and ArchPulse.sln are required.");
        if (testProjectRelative.Length > 300 ||
            Path.IsPathRooted(testProjectRelative) ||
            testProjectRelative.Contains('\\') ||
            !testProjectRelative.StartsWith("tests/", StringComparison.Ordinal) ||
            !testProjectRelative.EndsWith(".csproj", StringComparison.Ordinal) ||
            testProjectRelative.Split('/').Any(p => !PathSegment().IsMatch(p)) ||
            testProjectRelative.Split('/').Length < 3)
            throw new InvalidDataException("Only explicit relative tests/**/*.csproj paths are supported.");

        string project = Path.GetFullPath(Path.Combine(root,
            testProjectRelative.Replace('/', Path.DirectorySeparatorChar)));
        if (!project.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            !File.Exists(project))
            throw new InvalidDataException("Requested test project is not a regular file inside the trusted tree.");

        string assemblyRelative = Path.Combine(
            Path.GetDirectoryName(testProjectRelative)!, "bin", "Release", "net10.0",
            Path.GetFileNameWithoutExtension(testProjectRelative) + ".dll").Replace('\\', '/');
        string assembly = Path.GetFullPath(Path.Combine(root, assemblyRelative));
        if (!File.Exists(assembly))
            throw new InvalidDataException("Prebuilt conventional Release test DLL required; no build or restore is performed.");

        // Prevent accidental symlink escape via project, test assembly or intermediate folders.
        RejectLinks(root, project);
        RejectLinks(root, assembly);
        string output = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (output.Equals(root, StringComparison.Ordinal) ||
            output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("TRX output must remain outside the source repository.");
        if (!Directory.Exists(output) || Directory.EnumerateFileSystemEntries(output).Any() ||
            File.GetAttributes(output).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Use an existing empty non-symlink private output directory.");

        if (new FileInfo(assembly).Length is <= 0 or > MaxAssemblyBytes)
            throw new InvalidDataException("Prebuilt test assembly exceeds trusted capture bounds.");
        return new CapturePaths(root, testProjectRelative, project, assemblyRelative, assembly, output);
    }

    public static async Task<LocalTestRunReceipt> CaptureAsync(string repositoryRoot,
        string testProjectRelative, string outputDirectory,
        bool explicitlyAllowed, CancellationToken cancellationToken = default)
    {
        if (!explicitlyAllowed)
            throw new InvalidOperationException(
                "Use --allow-local-execution only on a trusted checkout. This runs existing test code, not a sandbox.");
        CapturePaths paths = ValidatePaths(repositoryRoot, testProjectRelative, outputDirectory);
        string source = await GitRevisionAndCleanAsync(paths.Root, cancellationToken);
        string assemblyHashBefore = Sha256BoundedFile(paths.AssemblyPath);
        var started = Stopwatch.StartNew();

        string trxPath = Path.Combine(paths.OutputDirectory, TrxFile);
        int exitCode;
        using (var runner = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, RedirectStandardError = true,
                RedirectStandardOutput = true, CreateNoWindow = true,
                WorkingDirectory = paths.Root
            }
        })
        {
            foreach (string arg in new[]
            {
                "test", paths.ProjectPath, "--no-restore", "--no-build", "-c", "Release",
                "--results-directory", paths.OutputDirectory,
                "--logger", "trx;LogFileName=" + TrxFile, "--verbosity", "quiet"
            })
                runner.StartInfo.ArgumentList.Add(arg);
            runner.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            runner.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
            runner.StartInfo.Environment["DOTNET_NOLOGO"] = "1";

            if (!runner.Start())
                throw new InvalidOperationException("Unable to start explicitly authorized local dotnet test.");
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(180));
                var outDrain = runner.StandardOutput.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
                var errDrain = runner.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
                await runner.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(outDrain, errDrain);
            }
            catch (OperationCanceledException)
            {
                if (!runner.HasExited) runner.Kill(entireProcessTree: true);
                throw new TimeoutException("Local test execution cancelled or exceeded three-minute limit.");
            }
            exitCode = runner.ExitCode;
        }
        started.Stop();

        string after = await GitRevisionAndCleanAsync(paths.Root, cancellationToken);
        if (after != source)
            throw new InvalidDataException("Git HEAD changed during local test run; receipt rejected.");
        string assemblyHashAfter = Sha256BoundedFile(paths.AssemblyPath);
        if (assemblyHashAfter != assemblyHashBefore)
            throw new InvalidDataException("Test output DLL mutated during local run; receipt rejected.");

        var run = TrxEvidenceReader.Import(trxPath, source, source);
        return new LocalTestRunReceipt(Schema, source,
            "GitRevParseAndCleanStatusBeforeAfterOnLocalCheckout",
            paths.ProjectRelative, paths.AssemblyRelative,
            assemblyHashBefore, assemblyHashAfter,
            "ConventionalPrebuiltOutputFileHashNotRunnerBinaryAttestation",
            TrxFile, run.TrxSha256, run.RunId,
            "DotnetTestNoRestoreNoBuildRelease",
            exitCode, "CliObservedLocalProcessExitAndImportedResultFile",
            started.ElapsedMilliseconds,
            run.AttemptCount, run.Counts,
            "TRXOutcomeAppliesToRunnerAttemptOnly",
            "NoSutCallOrReturnEvidence", false, false,
            [
                "Explicit --allow-local-execution runs locally trusted repository test code with current OS privileges: NOT sandboxed.",
                "Git HEAD/clean checks and conventional DLL SHA-256 are local observations, not proof that a runner loaded these bytes.",
                "TRX bytes/RunId are hashed and parsed but VSTest data is not cryptographically attested or independently authenticated.",
                "No method call/return spans, arguments, observed SUT method coverage, or test-case-to-Roslyn identity evidence."
            ]);
    }

    private static void RejectLinks(string root, string child)
    {
        string relative = Path.GetRelativePath(root, child);
        var current = root;
        if (File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Symlinked repository root is not allowed.");
        foreach (var component in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, component);
            if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Symlinks are not allowed in test project or assembly path.");
        }
    }

    private static string Sha256BoundedFile(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length is <= 0 or > MaxAssemblyBytes)
            throw new InvalidDataException("Invalid test assembly length.");
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static async Task<string> GitRevisionAndCleanAsync(string root, CancellationToken cancel)
    {
        string top = (await GitAsync(root, ["rev-parse", "--show-toplevel"], cancel)).TrimEnd('\n', '\r');
        if (Path.GetFullPath(top).TrimEnd(Path.DirectorySeparatorChar) != root)
            throw new InvalidDataException("Root is not the exact checkout top-level directory.");

        string commit = (await GitAsync(root, ["rev-parse", "--verify", "HEAD"], cancel)).Trim();
        if (!GitSha().IsMatch(commit))
            throw new InvalidDataException("Local HEAD is not an immutable full lowercase commit SHA.");

        string status = await GitAsync(root,
            ["status", "--porcelain=v1", "--untracked-files=all"], cancel);
        if (!string.IsNullOrWhiteSpace(status))
            throw new InvalidDataException("Checkout has tracked/untracked edits; refuse ambiguous source provenance.");
        return commit;
    }

    private static async Task<string> GitAsync(string root, string[] arguments, CancellationToken cancel)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, WorkingDirectory = root,
                CreateNoWindow = true
            }
        };
        foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) throw new InvalidDataException("Cannot inspect local Git repository.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            string result = await output;
            _ = await errors; // Never disclose Git output that could contain local host information.
            if (process.ExitCode != 0 || result.Length > 32_768)
                throw new InvalidDataException("Local Git inspection failed or exceeded bounds.");
            return result;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw new TimeoutException("Local Git source inspection timed out.");
        }
    }
}

public sealed record CapturePaths(string Root, string ProjectRelative, string ProjectPath,
    string AssemblyRelative, string AssemblyPath, string OutputDirectory);

public sealed record LocalTestRunReceipt(
    string Schema, string SourceGitCommit, string GitEvidence,
    string TestProjectRelative, string ConventionalTestAssemblyRelative,
    string ConventionalAssemblySha256Before, string ConventionalAssemblySha256After,
    string AssemblyEvidence, string TrxFilename, string TrxSha256, string TrxRunId,
    string Runner, int RunnerExitCode, string RunnerEvidence,
    long ObservedElapsedMilliseconds, int TestAttemptCount, TestResultCounts RunnerOutcomes,
    string TestOutcomeScope, string SutExecutionEvidence,
    bool SutMethodCallsObserved, bool PerTestCoverageProven,
    IReadOnlyList<string> Gaps);
