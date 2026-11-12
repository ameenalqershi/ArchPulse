// SPDX-License-Identifier: Apache-2.0
using System.Xml;
using System.Text.Json;
using ArchPulse.LicenseAudit;

if (args.Length < 2 || args[0] != "--repo")
{
    Console.Error.WriteLine("Usage: ArchPulse.LicenseAudit --repo <repo-root> [--packages <cache>] [--policy <file>] [--require-complete] [--output <json>]");
    return 2;
}
string repo = Path.GetFullPath(args[1]);
string? packages = null, policy = null, output = null;
bool requireComplete = false;
for (var i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--packages" when i + 1 < args.Length: packages = args[++i]; break;
        case "--policy" when i + 1 < args.Length: policy = args[++i]; break;
        case "--output" when i + 1 < args.Length: output = args[++i]; break;
        case "--require-complete": requireComplete = true; break;
        default: Console.Error.WriteLine($"Unexpected option: {args[i]}"); return 2;
    }
}
packages ??= Environment.GetEnvironmentVariable("NUGET_PACKAGES") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
policy ??= Path.Combine(repo, "docs", "operations", "nuget-license-policy.json");
try
{
    var report = NuGetLicenseAuditor.Audit(repo, packages, policy, requireComplete);
    string json = JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    });
    if (output is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, json + Environment.NewLine);
    }
    foreach (var entry in report.Packages)
        Console.WriteLine($"{entry.AuditStatus}: {entry.Package}@{entry.Version} " +
            $"({entry.LicenseExpression ?? entry.LegacyLicenseUrl ?? entry.LicenseFile ?? "no license metadata"})");
    foreach (var error in report.Errors)
        Console.Error.WriteLine("ERROR " + error);
    Console.WriteLine($"NuGet metadata: {report.Packages.Count} packages; " +
        $"{report.PendingManualReviews.Count} pending reviews; {report.Errors.Count} failures.");
    return report.Passed ? 0 : 1;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                           or JsonException or XmlException)
{
    Console.Error.WriteLine("NuGet metadata audit error: " + ex.Message);
    return 2;
}
