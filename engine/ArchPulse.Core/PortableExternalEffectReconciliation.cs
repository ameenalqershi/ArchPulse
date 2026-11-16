// SPDX-License-Identifier: Apache-2.0
namespace ArchPulse.Core;

// T058/S11: deterministic offline reconciliation of *observations*.
// This component never queries GitHub and never authorizes an external retry.
public enum EffectRecoveryDecision { Confirmed, Unknown, Diverged }

public sealed record PortableRemoteEffectObservation
{
    public required string TaskId { get; init; }
    public required string Action { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string State { get; init; } // Found, NotFound, Inconclusive
    public string? ReceiptSha256 { get; init; }
    public string? SourceRevision { get; init; }
}

public sealed record PortableEffectRecoveryResult(
    EffectRecoveryDecision Decision, bool SafeToRetry, string Reason);

public static class PortableExternalEffectReconciliation
{
    /// <summary>
    /// Evaluate an independently collected observation without causing a side effect.
    /// Never infer retry safety from an inconclusive or missing remote observation.
    /// </summary>
    public static PortableEffectRecoveryResult Evaluate(
        PortableExternalEffectReceipt local,
        PortableRemoteEffectObservation? remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        _ = PortableHandoffJson.ReadEffect(PortableHandoffJson.Write(local));
        if (remote is null)
            return Unknown("remote state has not been independently checked");
        if (remote.TaskId != local.TaskId || remote.Action != local.Action ||
            remote.IdempotencyKey != local.IdempotencyKey)
            return Diverged("observation belongs to a different external effect");
        if (remote.State is not ("Found" or "NotFound" or "Inconclusive"))
            return Diverged("observation has an unsupported state");
        if (remote.State == "Inconclusive")
            return Unknown("remote state is inconclusive; reconcile before any retry");
        if (remote.State == "NotFound")
            return Unknown("absence in one remote query is not proof of non-execution");
        if (remote.ReceiptSha256 is null || remote.ReceiptSha256.Length != 64 ||
            remote.ReceiptSha256.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            return Unknown("remote object has no trusted exact receipt digest");
        if (local.State == "Succeeded")
        {
            if (local.ReceiptSha256 != remote.ReceiptSha256)
                return Diverged("remote receipt differs from committed local success claim");
            return Confirmed("remote receipt digest matches the recorded success claim");
        }
        if (local.ReceiptSha256 is not null && local.ReceiptSha256 != remote.ReceiptSha256)
            return Diverged("remote receipt conflicts with local receipt");
        if (local.State == "Failed")
            return Diverged("remote object exists although local effect claimed failure");
        return Confirmed("remote object exists with a recorded digest; do not retry");
    }

    private static PortableEffectRecoveryResult Confirmed(string reason) =>
        new(EffectRecoveryDecision.Confirmed, false, reason);
    private static PortableEffectRecoveryResult Unknown(string reason) =>
        new(EffectRecoveryDecision.Unknown, false, reason);
    private static PortableEffectRecoveryResult Diverged(string reason) =>
        new(EffectRecoveryDecision.Diverged, false, reason);
}
