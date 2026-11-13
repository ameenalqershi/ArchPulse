# ProjectReference integrity guard (T005 / S01 partial)

This native C# repository check runs inside the existing offline foundation
validator; it scans tracked working-tree `.csproj` files without invoking
MSBuild, loading the solution, or executing a project's custom targets.

## What is checked

- Every literal `ProjectReference Include` must resolve to a local `.csproj`
  inside the repository. Backslash separators from Windows MSBuild paths are
  accepted on Linux.
- The resulting project-reference graph must have no cycles, including a
  self-reference. Errors identify relative project paths only.
- XML DTDs are prohibited, so XML entity declarations cannot be expanded.
- Symbolic-link project files are rejected rather than read outside the checkout.
- MSBuild expressions or wildcard references are **not evaluated**: their
  presence is an explicit validation failure, not guessed dependency evidence.
- Generated project files in `bin` and `obj`, and dependency directories,
  are skipped. This is a source-tree contract, not a built-artifact inspector.
- The root `ArchPulse.sln` is checked for valid C# project entries with existing
  `.csproj` files inside the checkout, unique project GUIDs and normalized paths.
  Solution folders are excluded from project-path checks. Absolute, dynamic,
  traversing and symlinked project paths (including symlink parents) are rejected.
  This does **not** require every example `.csproj` to be part of the root solution.

Run:

```bash
dotnet restore ArchPulse.sln --locked-mode
dotnet build ArchPulse.sln -c Release --no-restore
dotnet run --project tooling/ArchPulse.RepositoryValidator -c Release --no-build -- --root .
dotnet test tooling/ArchPulse.Tooling.Tests/ArchPulse.Tooling.Tests.csproj -c Release --no-build
```

The same `RepositoryChecker` is invoked by Foundation CI, so a newly added
broken direct reference, cycle or invalid root-solution project entry fails a PR.
Regression fixtures cover a valid DAG, Windows-style separators, dangling
references, root escape, cycles, self-cycles, XML DTD, unsupported MSBuild
expressions, generated directories, and malformed/duplicate/escaped/symlinked
solution entries.

## What this does not prove

This check neither performs MSBuild conditional evaluation nor enforces
**approved** context-level dependency directions. A valid DAG can still
violate architecture policy, and dynamic references cannot be proven safe from
syntax alone. No S01/T002 approval, module ownership, architecture Guardian
rule, runtime observation, or complete T005 acceptance is implied. Those
require a separately reviewed ADR and integration tests. See the
[architecture overview](../architecture/README.md) and
[task registry](../requirements/task-registry.md).
