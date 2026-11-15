// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using ArchPulse.LicenseAudit;
using Xunit;

namespace ArchPulse.LicenseAudit.Tests;

public sealed class NuGetLicenseAuditorTests
{
    [Fact]
    public void ExplicitAllowedSpdxMetadataPasses()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "MIT");
        var result = data.Audit();
        Assert.True(result.Passed, string.Join("; ", result.Errors));
        var entry = Assert.Single(result.Packages);
        Assert.Equal("metadata-accepted", entry.AuditStatus);
        Assert.Equal("MIT", entry.LicenseExpression);
        Assert.Empty(result.PendingManualReviews);
    }

    [Fact]
    public void UnknownLicenseExpressionFailsClosed()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "GPL-3.0-only");
        var result = data.Audit();
        Assert.False(result.Passed);
        Assert.Contains(result.Errors, error => error.Contains("Unreviewed", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyUrlOnlyReviewRemainsPendingAndBlocksStrictMode()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", null, null,
            "https://example.invalid/fixture-license");
        data.Review("fixture.one@1.2.3", "https://example.invalid/fixture-license");
        var regular = data.Audit();
        Assert.True(regular.Passed, string.Join("; ", regular.Errors));
        Assert.Equal("manual-review-pending", Assert.Single(regular.Packages).AuditStatus);
        Assert.Single(regular.PendingManualReviews);
        var strict = data.Audit(requireComplete: true);
        Assert.False(strict.Passed);
        Assert.Contains(strict.Errors, error => error.Contains("Unresolved manual review", StringComparison.Ordinal));
    }

    [Fact]
    public void ChangedLegacyLicenseUrlInvalidatesExistingException()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", null, null, "https://example.invalid/new");
        data.Review("fixture.one@1.2.3", "https://example.invalid/old");
        var result = data.Audit();
        Assert.False(result.Passed);
        Assert.Contains(result.Errors, error => error.Contains("absent or changed", StringComparison.Ordinal));
    }

    [Fact]
    public void ExceptionCannotOverrideExplicitNewExpression()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "GPL-3.0-only",
            "https://example.invalid/old");
        data.Review("fixture.one@1.2.3", "https://example.invalid/old");
        var result = data.Audit();
        Assert.False(result.Passed);
        Assert.Contains(result.Errors, error => error.Contains("cannot override", StringComparison.Ordinal));
    }

    [Fact]
    public void StaleExceptionIsRejected()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "MIT");
        data.Review("unrelated@9.9.9", "https://example.invalid/unused");
        var result = data.Audit();
        Assert.False(result.Passed);
        Assert.Contains(result.Errors, error => error.Contains("Stale", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingNuspecIsNotSilentlyTreatedAsPermitted()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "MIT");
        File.Delete(data.NuspecPath("fixture.one", "1.2.3"));
        Assert.Throws<FileNotFoundException>(() => data.Audit());
    }

    [Fact]
    public void NoCommittedLockfileIsRejected()
    {
        using var data = new Fixture();
        Assert.Throws<InvalidDataException>(() => data.Audit());
    }

    [Fact]
    public void XmlDtdIsRejectedRatherThanResolved()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "MIT");
        File.WriteAllText(data.NuspecPath("fixture.one", "1.2.3"),
            "<!DOCTYPE package [<!ENTITY secret SYSTEM 'file:///etc/passwd'>]><package><metadata><license type='expression'>&secret;</license></metadata></package>");
        Assert.Throws<System.Xml.XmlException>(() => data.Audit());
    }

    [Theory]
    [InlineData("../outside", "1.0.0")]
    [InlineData("fixture.one", "../../outside")]
    public void UnsafePackageCachePathsAreRejected(string package, string version)
    {
        using var data = new Fixture();
        data.AddPackage(package, version, "expression", "MIT", createNuspec: false);
        Assert.Throws<InvalidDataException>(() => data.Audit());
    }

    [Fact]
    public void DuplicatePackageIdsAcrossProjectsBecomeOneAuditEntry()
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "MIT");
        data.AddSecondLock();
        var result = data.Audit();
        Assert.True(result.Passed);
        Assert.Equal(2, Assert.Single(result.Packages).UsedBy.Count);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateLockfilePropertiesCannotBeSilentlyOverridden(bool nested)
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "MIT");
        string file = Path.Combine(data.Repo, "packages.lock.json");
        string content = File.ReadAllText(file);
        string target = nested ? "\"resolved\":\"1.2.3\"" : "\"dependencies\":";
        string replacement = nested
            ? "\"resolved\":\"0.0.0\",\"resolved\":\"1.2.3\""
            : "\"dependencies\":{},\"dependencies\":";
        Assert.Contains(target, content, StringComparison.Ordinal);
        File.WriteAllText(file, content.Replace(target, replacement, StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => data.Audit());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicatePolicyPropertiesCannotExpandAllowedLicenses(bool mixedCase)
    {
        using var data = new Fixture();
        data.AddPackage("fixture.one", "1.2.3", "expression", "MIT");
        string content = File.ReadAllText(data.Policy);
        string target = "\"acceptedSpdxExpressions\":";
        string replacement = (mixedCase
            ? "\"AcceptedSpdxExpressions\":[\"GPL-3.0-only\"],"
            : "\"acceptedSpdxExpressions\":[\"GPL-3.0-only\"],") + target;
        Assert.Contains(target, content, StringComparison.Ordinal);
        File.WriteAllText(data.Policy, content.Replace(target, replacement, StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => data.Audit());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _temp = Path.Combine(Path.GetTempPath(),
            "archpulse-license-test-" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, object> _pending = new();
        public string Repo => Path.Combine(_temp, "repo");
        public string Cache => Path.Combine(_temp, "packages");
        public string Policy => Path.Combine(_temp, "license-policy.json");

        public Fixture()
        {
            Directory.CreateDirectory(Repo);
            Directory.CreateDirectory(Cache);
            WritePolicy();
        }

        public string NuspecPath(string package, string version)
        {
            string lower = package.ToLowerInvariant();
            string p = Path.Combine(Cache, lower, version.ToLowerInvariant());
            return Path.Combine(p, lower + ".nuspec");
        }

        public void AddPackage(string package, string version, string? type,
            string? value, string? legacyUrl = null, bool createNuspec = true)
        {
            if (createNuspec)
            {
                string file = NuspecPath(package, version);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                var license = type is null ? "" : $"<license type='{type}'>{value}</license>";
                File.WriteAllText(file, "<package><metadata>" + license +
                    (legacyUrl is null ? "" : $"<licenseUrl>{legacyUrl}</licenseUrl>") +
                    "</metadata></package>");
            }
            WriteLock(Path.Combine(Repo, "packages.lock.json"), package, version);
        }

        public void AddSecondLock()
        {
            var second = Path.Combine(Repo, "tests", "Fixture.Tests");
            Directory.CreateDirectory(second);
            WriteLock(Path.Combine(second, "packages.lock.json"), "fixture.one", "1.2.3");
        }

        public void Review(string key, string source)
        {
            _pending.Add(key, new
            {
                issue = "https://example.invalid/issue",
                reason = "Human review of legacy URL metadata is outstanding",
                source
            });
            WritePolicy();
        }

        private void WritePolicy()
        {
            File.WriteAllText(Policy, JsonSerializer.Serialize(new
            {
                schema = "archpulse-nuget-license-policy/v1",
                acceptedSpdxExpressions = new[] { "Apache-2.0", "MIT" },
                pendingLegacyMetadataReviews = _pending
            }));
        }

        private static void WriteLock(string file, string package, string version)
        {
            File.WriteAllText(file, JsonSerializer.Serialize(new
            {
                version = 1,
                dependencies = new Dictionary<string, object>
                {
                    ["net10.0"] = new Dictionary<string, object>
                    {
                        [package] = new
                        {
                            type = "Direct",
                            requested = $"[{version}, )",
                            resolved = version,
                            contentHash = "test"
                        }
                    }
                }
            }));
        }

        public LicenseAuditReport Audit(bool requireComplete = false)
            => NuGetLicenseAuditor.Audit(Repo, Cache, Policy, requireComplete);

        public void Dispose()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }
    }
}
