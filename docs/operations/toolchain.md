# Toolchain

Founding developer platform: Ubuntu Linux VPS with .NET 10 SDK, Git, Python 3. No paid AI provider or API key required for local foundation tests. Lock the SDK via root `global.json` and NuGet dependencies via `packages.lock.json` for reproducible CI.

```bash
dotnet --info
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release
```

Never execute untrusted checkout projects with server admin secrets. Open-source runners and public GitHub Actions should use least-privilege tokens.
