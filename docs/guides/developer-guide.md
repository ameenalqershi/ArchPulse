# Developer guide

## Free toolchain

.NET 10 SDK, Git, a text editor and (for future web clients) Node.js LTS. No paid LLM key is required for the **current foundation** build or tests.

```bash
git clone https://github.com/ameenalqershi/ArchPulse.git
cd ArchPulse
dotnet --version
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet test ArchPulse.sln -c Release --no-build
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release
```

Review the [scope](../architecture/README.md), [specification index](../requirements/specification-index.md) and [task registry](../requirements/task-registry.md). Choose a Txxx; open an issue for implementation with Sxx links. Do not implement speculative network services or AI provider calls in the foundation without a reviewed task. Never commit credentials.
