// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Graph;

public sealed record StaticTestDeclaration(
    string TestMethodSymbolId, string TestProject, string Framework, SourceEvidence Evidence,
    string DiscoveryEvidence, string CaseGranularity, string LastResult);

public sealed record StaticMethodTestLink(
    string TestMethodSymbolId, string TargetSymbolId, string TestProject, string Framework,
    string Relation, string Verification, string ExecutionEvidence, string LastResult,
    SourceEvidence CallSite);

public sealed record StaticMethodTestAssociation(
    string TestMethodSymbolId, string TargetSymbolId, string TestProject, string Framework,
    string Relation, string Verification, string ExecutionEvidence, string LastResult,
    IReadOnlyList<SourceEvidence> CallSites);

public sealed record StaticTestAssociationReport(
    string Schema, string GraphProject, string GraphRevision, string MappingScope,
    string RunnerDiscovery, string ResultEvidence,
    IReadOnlyList<StaticTestDeclaration> TestDeclarations,
    IReadOnlyList<StaticMethodTestAssociation> Associations,
    IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<StaticMethodTestAssociation> ForTarget(string targetSymbolId) =>
        Associations.Where(a => a.TargetSymbolId == targetSymbolId).ToArray();
}
