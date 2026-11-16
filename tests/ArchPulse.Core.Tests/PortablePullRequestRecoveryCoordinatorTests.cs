// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using ArchPulse.Core;
using Xunit;

namespace ArchPulse.Core.Tests;

public sealed class PortablePullRequestRecoveryCoordinatorTests
{
    private const string Owner = "ameenalqershi";
    private const string Repository = "ArchPulse";
    private static readonly string HeadSha = new('a', 40);

    private static PortableExternalEffectReceipt Interrupted() => new()
    {
        Schema = "archpulse.external-effect-receipt/v1",
        TaskId = "T058",
        Action = "github.create-pr",
        IdempotencyKey = "create-commit-a",
        State = "Unknown",
        ReconciliationStep = "Check whether create succeeded before retry"
    };

    [Theory]
    [InlineData("found", EffectRecoveryDecision.Confirmed)]
    [InlineData("absent", EffectRecoveryDecision.Unknown)]
    [InlineData("failed", EffectRecoveryDecision.Unknown)]
    [InlineData("invalid", EffectRecoveryDecision.Unknown)]
    public async Task InterruptedCreateNeverProducesSecondCreate(
        string scenario, EffectRecoveryDecision expected)
    {
        var handler = new FakeGitHub(scenario);
        using var client = new HttpClient(handler);
        var result = await PortablePullRequestRecoveryCoordinator.ReconcileAsync(
            client, Interrupted(), Owner, Repository, "feat/branch", "main", HeadSha);
        Assert.Equal(expected, result.Decision);
        Assert.False(result.SafeToRetry);
        Assert.Equal(1, handler.ReadRequests);
        Assert.Equal(0, handler.WriteRequests);
    }

    [Fact]
    public async Task StaleRemoteHeadIsDivergedRatherThanRecreated()
    {
        var handler = new FakeGitHub("stale");
        using var client = new HttpClient(handler);
        var result = await PortablePullRequestRecoveryCoordinator.ReconcileAsync(
            client, Interrupted(), Owner, Repository, "feat/branch", "main", HeadSha);
        Assert.Equal(EffectRecoveryDecision.Diverged, result.Decision);
        Assert.False(result.SafeToRetry);
        Assert.Equal(0, handler.WriteRequests);
    }

    [Fact]
    public async Task CancellationRemainsObservableAndDoesNotMutate()
    {
        var handler = new FakeGitHub("found");
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PortablePullRequestRecoveryCoordinator.ReconcileAsync(client, Interrupted(),
                Owner, Repository, "feat/branch", "main", HeadSha, cancellation.Token));
        Assert.Equal(0, handler.WriteRequests);
    }

    private sealed class FakeGitHub(string scenario) : HttpMessageHandler
    {
        public int WriteRequests { get; private set; }
        public int ReadRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Method != HttpMethod.Get) WriteRequests++;
            else ReadRequests++;
            var response = scenario switch
            {
                "failed" => new HttpResponseMessage(HttpStatusCode.BadGateway),
                "invalid" => new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{not-json") },
                "absent" => new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("[]") },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new[]
                    {
                        new
                        {
                            number = 27,
                            head = new
                            {
                                @ref = "feat/branch",
                                sha = scenario == "stale" ? new string('b', 40) : HeadSha,
                                repo = new { full_name = Owner + "/" + Repository }
                            },
                            @base = new
                            {
                                @ref = "main",
                                repo = new { full_name = Owner + "/" + Repository }
                            }
                        }
                    }))
                }
            };
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
