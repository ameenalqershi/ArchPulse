// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ArchPulse.LicenseAudit;

public sealed record LegacyReview(string Issue, string Reason, string Source);
public sealed record LicensePolicy(
    string Schema,
    IReadOnlyList<string> AcceptedSpdxExpressions,
    Dictionary<string, LegacyReview> PendingLegacyMetadataReviews);
public sealed record LicenseMetadata(
    string? LicenseExpression, string? LicenseFile, string? LegacyLicenseUrl);
public sealed record PackageRecord(string Package, string Version, IReadOnlyList<string> UsedBy,
    string? LicenseExpression, string? LicenseFile, string? LegacyLicenseUrl,
    string AuditStatus, string? ReviewIssue);
public sealed record LicenseAuditReport(string Schema, string Scope,
    IReadOnlyList<PackageRecord> Packages, IReadOnlyList<string> PendingManualReviews,
    IReadOnlyList<string> Errors, bool Passed);

/// <summary>
/// Reads restored NuGet .nuspec metadata from an authorized local package cache.
/// SPDX expressions are policy inputs, not statements of legal compatibility.
/// Never follows license URLs or downloads arbitrary files.
/// </summary>
public static partial class NuGetLicenseAuditor
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.+\-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentity();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static LicenseAuditReport Audit(string repositoryRoot, string packagesCacheRoot,
        string policyFile, bool requireComplete = false)
    {
        var repo = Path.GetFullPath(repositoryRoot);
        var cache = Path.GetFullPath(packagesCacheRoot);
        var policy = JsonSerializer.Deserialize<LicensePolicy>(File.ReadAllText(policyFile), JsonOptions)
            ?? throw new InvalidDataException("NuGet license policy is empty.");
        if (policy.Schema != "archpulse-nuget-license-policy/v1" ||
            policy.AcceptedSpdxExpressions is null || policy.PendingLegacyMetadataReviews is null)
            throw new InvalidDataException("Unknown or incomplete NuGet license policy schema.");

        var permitted = policy.AcceptedSpdxExpressions.ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();
        var observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new SortedDictionary<string, (string Package, string Version, List<string> UsedBy)>(
            StringComparer.OrdinalIgnoreCase);
        var lockFiles = Directory.EnumerateFiles(repo, "packages.lock.json",
            SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(repo, p).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" or ".git" or "node_modules"))
            .Order(StringComparer.Ordinal).ToArray();
        if (lockFiles.Length == 0)
            throw new InvalidDataException("No committed NuGet packages.lock.json were found.");

        foreach (var file in lockFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("dependencies", out var dependencies) ||
                dependencies.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Missing lockfile dependencies: {file}");
            foreach (var target in dependencies.EnumerateObject())
            {
                foreach (var package in target.Value.EnumerateObject())
                {
                    if (package.Value.TryGetProperty("type", out var packageType)
                        && packageType.GetString() == "Project") continue;
                    if (!package.Value.TryGetProperty("resolved", out var v) ||
                        v.ValueKind != JsonValueKind.String)
                        throw new InvalidDataException($"Missing resolved version for {package.Name}");
                    string version = v.GetString()!;
                    if (!SafeIdentity().IsMatch(package.Name) || !SafeIdentity().IsMatch(version)
                        || package.Name is "." or ".." || version is "." or "..")
                        throw new InvalidDataException($"Unsafe package identity: {package.Name}@{version}");
                    string id = $"{package.Name.ToLowerInvariant()}@{version.ToLowerInvariant()}";
                    if (!packages.TryGetValue(id, out var entry))
                        entry = (package.Name, version, []);
                    entry.UsedBy.Add(Path.GetRelativePath(repo, file).Replace('\\', '/') +
                        ":" + target.Name + ":" + (packageType.ValueKind == JsonValueKind.String
                            ? packageType.GetString() : "Unknown"));
                    packages[id] = entry;
                }
            }
        }
        var results = new List<PackageRecord>();
        foreach (var (key, entry) in packages)
        {
            var meta = ReadNuspec(cache, entry.Package, entry.Version);
            var expression = meta.LicenseExpression;
            var status = "unreviewed";
            string? reviewIssue = null;
            if (expression is not null && permitted.Contains(expression))
                status = "metadata-accepted";
            else if (policy.PendingLegacyMetadataReviews.TryGetValue(key, out var review))
            {
                if (string.IsNullOrWhiteSpace(review.Issue) ||
                    string.IsNullOrWhiteSpace(review.Reason) ||
                    string.IsNullOrWhiteSpace(review.Source))
                    failures.Add($"Malformed pending review for {key}");
                if (expression is not null || meta.LicenseFile is not null)
                    failures.Add($"Legacy URL-only exception cannot override declared license: {key}");
                if (string.IsNullOrWhiteSpace(meta.LegacyLicenseUrl) ||
                    meta.LegacyLicenseUrl != review.Source)
                    failures.Add($"Legacy license URL absent or changed: {key}");
                status = "manual-review-pending";
                reviewIssue = review.Issue;
                observed.Add(key);
                if (requireComplete)
                    failures.Add($"Unresolved manual review: {key}");
            }
            else
            {
                failures.Add($"Unreviewed NuGet license metadata: {key} " +
                             $"({expression ?? meta.LegacyLicenseUrl ?? meta.LicenseFile ?? "none"})");
            }
            results.Add(new PackageRecord(entry.Package, entry.Version,
                entry.UsedBy.Order(StringComparer.Ordinal).ToArray(),
                expression, meta.LicenseFile, meta.LegacyLicenseUrl, status, reviewIssue));
        }
        foreach (var key in policy.PendingLegacyMetadataReviews.Keys.Order(StringComparer.Ordinal))
            if (!observed.Contains(key))
                failures.Add($"Stale package license exception: {key}");

        return new LicenseAuditReport("archpulse-nuget-license-audit/v1",
            "Restored NuGet metadata only; excludes bundled assets, JS packages and runtime licensing",
            results, observed.Order(StringComparer.Ordinal).ToArray(),
            failures.Order(StringComparer.Ordinal).ToArray(), failures.Count == 0);
    }

    public static LicenseMetadata ReadNuspec(string cacheRoot, string package, string version)
    {
        if (!SafeIdentity().IsMatch(package) || !SafeIdentity().IsMatch(version) ||
            package is "." or ".." || version is "." or "..")
            throw new InvalidDataException("Unsafe NuGet package identity.");
        var lowerPackage = package.ToLowerInvariant();
        var lowerVersion = version.ToLowerInvariant();
        string path = Path.Combine(Path.GetFullPath(cacheRoot),
            lowerPackage, lowerVersion, lowerPackage + ".nuspec");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Missing cached .nuspec: {package}@{version}. Restore first.", path);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 4_000_000, IgnoreComments = true
        };
        using var reader = XmlReader.Create(path, settings);
        var xml = XDocument.Load(reader);
        var metadata = xml.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata")
            ?? throw new InvalidDataException($"Missing NuGet metadata: {package}@{version}");
        var license = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "license");
        var type = license?.Attribute("type")?.Value;
        string? value = string.IsNullOrWhiteSpace(license?.Value) ? null : license.Value.Trim();
        var legacy = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "licenseUrl");
        return new LicenseMetadata(
            type == "expression" ? value : null,
            type == "file" ? value : null,
            string.IsNullOrWhiteSpace(legacy?.Value) ? null : legacy.Value.Trim());
    }
}
