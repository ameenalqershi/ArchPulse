// SPDX-License-Identifier: Apache-2.0
using ArchPulse.TraceabilityValidator;

var root = args.Length switch
{
    0 => RepositoryRoot.Find(AppContext.BaseDirectory),
    2 when args[0] == "--root" => Path.GetFullPath(args[1]),
    _ => throw new ArgumentException("Usage: ArchPulse.TraceabilityValidator [--root /path/to/repository]")
};
var errors = TraceabilityChecker.Validate(root);
foreach (var error in errors)
    Console.Error.WriteLine($"ERROR {error}");
Console.WriteLine(errors.Count == 0
    ? "TRACEABILITY INDEX VALIDATED — proposed registry consistency, NOT product acceptance"
    : $"TRACEABILITY VALIDATION FAILED ({errors.Count} errors)");
return errors.Count == 0 ? 0 : 1;
