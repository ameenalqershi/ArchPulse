// SPDX-License-Identifier: Apache-2.0
using ArchPulse.RepositoryValidator;
using ArchPulse.TraceabilityValidator;

bool adrOnly = args.Contains("--adr", StringComparer.Ordinal);
bool handoffsOnly = args.Contains("--handoffs", StringComparer.Ordinal);
if (adrOnly && handoffsOnly)
    throw new ArgumentException("Choose only one validation scope.");
var parameters = args.Where(x => x is not ("--adr" or "--handoffs")).ToArray();
var root = parameters.Length switch
{
    0 => RepositoryRoot.Find(AppContext.BaseDirectory),
    2 when parameters[0] == "--root" => Path.GetFullPath(parameters[1]),
    _ => throw new ArgumentException("Usage: ArchPulse.RepositoryValidator [--root /path/to/repository] [--adr|--handoffs]")
};
var errors = adrOnly ? AdrChecker.Validate(root) :
    handoffsOnly ? HandoffAllChecker.Validate(root) : RepositoryChecker.Validate(root);
foreach (var error in errors)
    Console.Error.WriteLine($"ERROR {error}");
Console.WriteLine(errors.Count == 0
    ? (adrOnly ? "ADR CONTRACT VALIDATED — NOT proof of human review/approval" :
       handoffsOnly ? "ALL HANDOFF CHECKPOINTS VALIDATED — NOT evidence of external effects" :
       "FOUNDATION STRUCTURE VALIDATED — NOT proof of product functionality")
    : (adrOnly ? $"ADR VALIDATION FAILED ({errors.Count} errors)" :
       handoffsOnly ? $"HANDOFF VALIDATION FAILED ({errors.Count} errors)" :
       $"FOUNDATION VALIDATION FAILED ({errors.Count} errors)"));
return errors.Count == 0 ? 0 : 1;
