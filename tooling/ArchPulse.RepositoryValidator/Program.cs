// SPDX-License-Identifier: Apache-2.0
using ArchPulse.Tooling;

var root = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
var errors = FoundationValidator.Validate(Path.GetFullPath(root));
foreach (var error in errors) Console.Error.WriteLine(error);
if (errors.Count == 0)
{
    Console.WriteLine("REPOSITORY FOUNDATION VALIDATED; not proof of product implementation.");
    return 0;
}
return 1;
