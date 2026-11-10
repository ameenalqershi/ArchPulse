// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Core;

/// <summary>Classification of engineering claims; none alone guarantees correctness.</summary>
public enum EvidenceKind
{
    Unknown,
    Proposed,
    Static,
    Observed,
    Inferred,
    Verified
}

/// <summary>Foundational contract; real-world source authenticity must be independently checked.</summary>
public sealed record EvidenceReceipt
{
    private EvidenceReceipt(EvidenceKind kind, string? source, string? revision)
    {
        Kind = kind;
        Source = source;
        Revision = revision;
    }

    public EvidenceKind Kind { get; }
    public string? Source { get; }
    public string? Revision { get; }

    public static EvidenceReceipt Create(EvidenceKind kind, string? source, string? revision)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (kind == EvidenceKind.Verified &&
            (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(revision)))
        {
            throw new ArgumentException("A verified claim requires source and revision, plus independent validation.");
        }
        return new EvidenceReceipt(kind, source, revision);
    }
}
