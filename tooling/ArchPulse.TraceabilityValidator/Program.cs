// SPDX-License-Identifier: Apache-2.0
using ArchPulse.TraceabilityValidator;

bool baseline = args.Contains("--baseline", StringComparer.Ordinal);
var parameters = args.Where(x => x != "--baseline").ToArray();
var root = parameters.Length switch
{
    0 => RepositoryRoot.Find(AppContext.BaseDirectory),
    2 when parameters[0] == "--root" => Path.GetFullPath(parameters[1]),
    _ => throw new ArgumentException("Usage: ArchPulse.TraceabilityValidator [--root /path/to/repository] [--baseline]")
};
var errors = baseline ? RequirementBaselineChecker.Validate(root) : TraceabilityChecker.Validate(root);
foreach (var error in errors)
    Console.Error.WriteLine($"ERROR {error}");
Console.WriteLine(errors.Count == 0
    ? (baseline ? "REQUIREMENT BASELINE VALIDATED — proposed snapshot, NOT implementation acceptance"
                : "TRACEABILITY INDEX VALIDATED — proposed registry consistency, NOT product acceptance")
    : (baseline ? $"REQUIREMENT BASELINE FAILED ({errors.Count} errors)"
                : $"TRACEABILITY VALIDATION FAILED ({errors.Count} errors)"));
return errors.Count == 0 ? 0 : 1;
