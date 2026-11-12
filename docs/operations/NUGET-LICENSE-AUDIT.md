# Offline NuGet license metadata audit — T104 / S19

**State:** an automated package-metadata inventory and change gate, **not** an accepted legal clearance or software bill of materials. See [T104 issue #10](https://github.com/ameenalqershi/ArchPulse/issues/10).

The source list is all restored direct and transitive NuGet package versions in committed `packages.lock.json` files. The scanner reads package `.nuspec` records from the local NuGet cache, **does not download arbitrary license URLs**, and compares explicit SPDX license expressions with the narrowly reviewed `acceptedSpdxExpressions` policy. A nonmatching expression, missing package metadata, stale exception, or previously unseen URL-only license fails the default gate.

A legacy metadata exception applies only to a URL-only package whose current `licenseUrl` exactly matches its recorded policy `source`. New SPDX license declarations, license files, missing URLs or changed URLs must fail the gate even if the package ID/version is on the exception list. A matching URL is metadata consistency, **not** source-code license verification.

A **documented legacy metadata exception** may keep the *metadata-change* gate green while showing a nonzero `pendingManualReviews` count. This is not a conclusion that its license is compatible. It must have an issue, a reason, and the declared primary source for later verification. The stricter `--require-complete` mode deliberately fails while unresolved reviews exist and is a prerequisite to claiming a complete license review or production release readiness.

The canonical audit is now implemented in **C#/.NET** with 12 initial xUnit contract tests, pinned package resolution and a no-network cache reader. The earlier Python script is retained as an optional migration reference, not the engine or release authority.

## Reproduce

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet run --project tooling/ArchPulse.LicenseAudit -c Release --no-build -- --repo . --output /tmp/archpulse-nuget-license-audit.json
dotnet run --project tooling/ArchPulse.LicenseAudit -c Release --no-build -- --repo . --require-complete  # expected to fail until reviewed
dotnet test ArchPulse.sln -c Release --no-build
```

The current tracked foundation includes one URL-only transitive package, `xunit.abstractions@2.0.3`, and its review remains open in [issue #10](https://github.com/ameenalqershi/ArchPulse/issues/10). It is intentionally **not** automatically equated to another package's Apache-2.0 metadata. Review the version-specific original license and distribution obligations, document the actual evidence, and then resolve/remove the exception without falsifying the package's metadata.

This test does **not** cover non-NuGet source code, JS packages, editors/extensions, icon or font assets, SDK runtimes, container base images, hosted external components, contributor licensing, notices, or commercial trademark permissions. These need separate per-release review. `LICENSE` remains Apache License 2.0 for ArchPulse-owned source; third-party license terms are not overwritten.

Reference: [third-party policy](third-party-policy.md), [Foundation CI](CI-GATES.md), [S19](https://app.notion.com/p/4143830ba34d817e8ddbf1dde897ee2f).
