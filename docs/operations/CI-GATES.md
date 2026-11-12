# Native .NET Foundation CI — 2026-11-12

The two current required GitHub checks are named foundation-build-test and foundation-document-check, enforced by main ruleset 26926696. The initial native .NET workflow succeeded on [run 43248347098](https://github.com/ameenalqershi/ArchPulse/actions/runs/43248347098) for PR #31 commit ae8272ba712308280f498ea044503d426587c7f3. This validates the native CI path on that exact revision, not production features or any later commit.

## Actual gates
- foundation-build-test: pinned checkout and .NET 10 SDK actions; locked restore, Release build, all C# unit/integration tests (28 locally), pinned Node 24 setup and locked Web Studio TypeScript production build.
- foundation-document-check: pinned checkout and SDK setup; locked restore/build; .NET RepositoryValidator for files/licenses/SPDX/links and .NET TraceabilityValidator for historical S/T/R registry references, DAG and statuses.
- GitHub Actions permissions: contents read; checkout credentials not persisted; no Python runtime required by either job.

## Future unimplemented gates
Complete architecture Guardian policy enforcement, security and license audit, SBOM, secrets, untrusted-code sandbox, endpoint auth, runtime tracing, MCP integration, Futures, production release and multi-repository E2E performance validation remain distinct work. Other CodeQL jobs may exist independently and must be evaluated on each exact commit.

## Reproduce locally
```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build --no-restore
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build
dotnet run --project tooling/ArchPulse.TraceabilityValidator -c Release --no-build
(cd apps/web-studio && npm ci --ignore-scripts && npm run build)
```

Never add newly required check names to main ruleset before verifying an actual run. The pre-rebuild [run 42758731375](https://github.com/ameenalqershi/ArchPulse/actions/runs/42758731375) was Python-based and is historical evidence only.
