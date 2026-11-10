# Foundation CI and future quality gates

**Current evidence as of 2026-11-10**: `Foundation CI` defines `foundation-build-test` and `foundation-document-check`. Both jobs passed in [GitHub Actions run 42758731375](https://github.com/ameenalqershi/ArchPulse/actions/runs/42758731375) on SHA `3c694ac44f445fba60c9e892e76ca96eb3022725`. New pull-request commits require fresh checks. They are not a product release certificate.

## Existing jobs

- `foundation-build-test`: locked NuGet restore, .NET Release build, four existing evidence-receipt unit tests.
- `foundation-document-check`: structural and link checks from `scripts/validate_foundation.py`; independent Python standard-library contract tests are added by the foundation audit.
- GitHub token permission is `contents: read`; checkout credentials are not persisted.

## Pending PR: NuGet license metadata change gate (T104)

A proposed CI step runs `python3 scripts/audit_nuget_licenses.py` after locked restore in `foundation-build-test`. It rejects newly unreviewed licenses and records an explicit legacy-metadata exception for `xunit.abstractions@2.0.3`. This is **not a full license-compliance certification**; `--require-complete` still fails until review. The change is not an active GitHub gate until the Pull Request checks have completed successfully.

## Intended additional gates (not all implemented)

- Static analyzers, formatting checks, architecture policy conformance, dependency license audit and SBOM.
- Security scans (CodeQL or approved equivalent, secrets, dependency vulnerabilities), with one authoritative CodeQL configuration.
- Integration tests in ephemeral isolated workers, regression/cross-platform suites, benchmark baselines and accessibility checks.
- Release signing and artifact provenance; no signing keys or uploads in untrusted fork PRs.

**Never declare any of these checks active or green until the workflow and results have been independently observed.** Before adding required checks to branch rules, run them on a PR, inspect their names, pass states and trust boundaries, then update policy without locking contributors out.

## Local reproducibility

```bash
python3 scripts/validate_foundation.py
python3 -m unittest discover -s scripts -p 'test_*.py' -v
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build --no-restore
```

References: [Quality Strategy](../architecture/quality-strategy.md), S16, S17, S19 and S20.
