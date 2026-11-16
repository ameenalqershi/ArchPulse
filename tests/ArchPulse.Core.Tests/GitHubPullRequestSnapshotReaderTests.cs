// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text;
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class GitHubPullRequestSnapshotReaderTests
{
    private static readonly string Sha = new('a', 40);
    private const string Repo = "ameenalqershi/ArchPulse";
    private static string Pull(int number, string source = Repo) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            number,
            head = new { @ref = "feat/recovery", sha = Sha, repo = new { full_name = source } },
            @base = new { @ref = "main", repo = new { full_name = Repo } }
        });

    [Fact]
    public async Task ReadsAllStatesAndFindsUniqueSameRepoPr()
    {
        var handler = new ReplyHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("[" + Pull(12) + "]") });
        using var client = new HttpClient(handler);
        var result = await GitHubPullRequestSnapshotReader.ReadAsync(client, "ameenalqershi", "ArchPulse");
        Assert.True(result.QueryComplete);
        Assert.Single(result.Candidates);
        Assert.Equal(12, result.Candidates[0].Number);
        Assert.Contains("state=all", handler.LastUri!.Query);
        Assert.Equal(EffectRecoveryDecision.Confirmed, PortablePullRequestRecovery.Evaluate(
            new PortableExternalEffectReceipt
            {
                Schema = "archpulse.external-effect-receipt/v1",
                TaskId = "T058",
                Action = "github.create-pr",
                IdempotencyKey = "create-pr-01",
                State = "Unknown",
                ReconciliationStep = "Reconcile"
            }, "https://github.com/ameenalqershi/ArchPulse", "feat/recovery", "main", Sha,
            result).Decision);
    }

    [Fact]
    public async Task ForkPrCannotImpersonateLocalHead()
    {
        using var client = new HttpClient(new ReplyHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("[" + Pull(12, "attacker/fork") + "]") }));
        var result = await GitHubPullRequestSnapshotReader.ReadAsync(client, "ameenalqershi", "ArchPulse");
        Assert.True(result.QueryComplete);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task MissingFieldsAndDuplicateNumbersFailClosed()
    {
        using var missingClient = new HttpClient(new ReplyHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{}]") }));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GitHubPullRequestSnapshotReader.ReadAsync(missingClient, "ameenalqershi", "ArchPulse"));

        using var duplicateClient = new HttpClient(new ReplyHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[" + Pull(1) + "," + Pull(1) + "]") }));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GitHubPullRequestSnapshotReader.ReadAsync(duplicateClient, "ameenalqershi", "ArchPulse"));
    }

    [Fact]
    public async Task FailureIsNeverSilentlyReportedAsEmptyList()
    {
        using var client = new HttpClient(new ReplyHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            GitHubPullRequestSnapshotReader.ReadAsync(client, "ameenalqershi", "ArchPulse"));
    }

    [Fact]
    public async Task EightFullPagesCannotClaimCompleteListing()
    {
        var body = "[" + string.Join(",", Enumerable.Range(1, 100).Select(n => Pull(n))) + "]";
        var count = 0;
        using var client = new HttpClient(new ReplyHandler((_, _) =>
        {
            count++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GitHubPullRequestSnapshotReader.ReadAsync(client, "ameenalqershi", "ArchPulse"));
        Assert.Equal(2, count); // Identical pages contain duplicate PR numbers.
    }

    [Fact]
    public async Task RejectsMalformedRepositoryBeforeRemoteCall()
    {
        using var client = new HttpClient(new ReplyHandler((_, _) =>
            throw new InvalidOperationException("Must not call the network.")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            GitHubPullRequestSnapshotReader.ReadAsync(client, "a/b", "ArchPulse"));
    }

    private sealed class ReplyHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            var response = handler(request, ct);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
