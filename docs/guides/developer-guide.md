# Developer guide

## Free, reproducible .NET setup

The current local-first foundation requires the **.NET 10 SDK** and Git. No paid AI subscription, API key, maintainer permissions or running cloud service is needed to build and execute the **current .NET solution tests**. Node.js/npm are required separately for Web Studio; .NET commands alone do not exercise the browser UI.

Use an ordinary, non-administrator account and a writable directory. Compare `dotnet --version` with the SDK specified in [global.json](../../global.json). If .NET 10 is missing, install the free SDK from the official .NET downloads. Use `dotnet --list-sdks` to investigate version selection. First-time restore normally needs network access to download locked NuGet packages; offline use requires the packages to be cached.

### Clone without maintainer access

You can clone the public upstream repository just to reproduce the checks. To submit a Pull Request, fork it into your own account, then clone **your fork** and work on a topic branch; do not push to upstream `main`.

```bash
git clone https://github.com/ameenalqershi/ArchPulse.git
cd ArchPulse
git rev-parse HEAD
dotnet --version
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root .
```

Run every command from the **repository root**, in order. The arguments after `--` belong to the native validator; `--root .` selects the checkout explicitly. A successful validation prints `FOUNDATION STRUCTURE VALIDATED`. This proves checked repository structure, **not** product readiness or full Sxx/Txxx acceptance. Record the exact Git revision, OS, SDK version and command exit codes in the PR; redact all local private paths, tokens and personal data.

### Troubleshooting and boundaries

- A failed locked restore may mean the SDK is missing, a feed is unavailable, or lockfiles differ. **Do not delete lockfiles or bypass `--locked-mode`** to manufacture a green result.
- If the directory or package cache is not writable, move the checkout or set `DOTNET_CLI_HOME` and `NUGET_PACKAGES` to directories owned by your account; do not work around it with `sudo dotnet`.
- Web Studio requires a separate Node.js/npm setup and its own build/tests. Successful .NET tests are not evidence of browser UI or MCP transport acceptance.
- Include only a short redacted error excerpt and the exact failing command when requesting help. Never post credentials, `gh auth` output, unredacted environment dumps or home-directory paths.

Review the [scope](../architecture/README.md), [specification index](../requirements/specification-index.md), [task registry](../requirements/task-registry.md), and [native project-reference integrity guard](project-reference-guard.md) (T005/S01 partial). Choose an existing Txxx and its Sxx before implementing a feature. Do not add speculative network services or paid AI-provider calls without review. Never commit credentials.
