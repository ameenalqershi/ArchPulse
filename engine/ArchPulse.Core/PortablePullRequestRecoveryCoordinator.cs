// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
namespace ArchPulse.Core;

/// <summary>
/// T058 experimental, read-only reconciliation after an interrupted PR creation.
/// It never calls a mutating endpoint or grants authority to retry.
/// </summary>
public static class PortablePullRequestRecoveryCoordinator
{
    public static async Task<PortableEffectRecoveryResult> ReconcileAsync(
        HttpClient githubClient, PortableExternalEffectReceipt receipt,
        string owner, string repository, string headBranch, string baseBranch,
        string expectedHeadCommit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Action != "github.create-pr")
            throw new ArgumentException("Only PR creation can be reconciled.", nameof(receipt));
        var expectedRepository = $"https://github.com/{owner}/{repository}";
        try
        {
            var snapshot = await GitHubPullRequestSnapshotReader.ReadAsync(
                githubClient, owner, repository, cancellationToken).ConfigureAwait(false);
            return PortablePullRequestRecovery.Evaluate(receipt, expectedRepository,
                headBranch, baseBranch, expectedHeadCommit, snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new PortableEffectRecoveryResult(EffectRecoveryDecision.Unknown,
                false, "GitHub observation failed; never retry a potentially committed create.");
        }
        catch (Exception error) when (error is InvalidDataException or JsonException)
        {
            return new PortableEffectRecoveryResult(EffectRecoveryDecision.Unknown,
                false, "GitHub observation was untrustworthy; manual reconciliation required.");
        }
    }
}
