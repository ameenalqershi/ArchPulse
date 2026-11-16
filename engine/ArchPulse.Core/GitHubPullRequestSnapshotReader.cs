// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;

namespace ArchPulse.Core;

/// <summary>
/// Read-only GitHub REST PR discovery, T058 experimental. Caller owns the HTTP
/// client and its credentials. Never creates or retries an external effect.
/// </summary>
public static class GitHubPullRequestSnapshotReader
{
    private const int PageSize = 100;
    private const int MaxPages = 8;

    public static async Task<PortablePullRequestSnapshot> ReadAsync(
        HttpClient client, string owner, string repo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (!ValidSegment(owner) || !ValidSegment(repo))
            throw new ArgumentException("Invalid GitHub repository owner/name.");

        var candidates = new List<PortablePullRequestCandidate>();
        var numbers = new HashSet<int>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var endpoint = new Uri(
                $"https://api.github.com/repos/{owner}/{repo}/pulls?state=all&per_page={PageSize}&page={page}");
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd("ArchPulse-T058-ReadOnly/1.0");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            // Redirects are not trustworthy as evidence: the requested URL and final URL
            // must stay on the canonical API host.
            if (response.RequestMessage?.RequestUri is not { Scheme: "https", Host: "api.github.com" })
                throw new InvalidDataException("PR response origin must be canonical GitHub API.");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException("GitHub PR query did not succeed.", null, response.StatusCode);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var parsed = await JsonDocument.ParseAsync(stream,
                new JsonDocumentOptions { MaxDepth = 16 }, cancellationToken).ConfigureAwait(false);
            if (parsed.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("GitHub PR response must be an array.");
            var items = parsed.RootElement.EnumerateArray().ToArray();
            if (items.Length > PageSize)
                throw new InvalidDataException("GitHub PR response exceeds requested page size.");
            foreach (var item in items)
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("number", out var numberElement) ||
                    !numberElement.TryGetInt32(out var number) || number <= 0 ||
                    !numbers.Add(number))
                    throw new InvalidDataException("GitHub PR identity is invalid or repeated.");
                var head = RequiredObject(item, "head");
                var baseRef = RequiredObject(item, "base");
                var sourceRepository = RequiredString(RequiredObject(head, "repo"), "full_name");
                var destinationRepository = RequiredString(RequiredObject(baseRef, "repo"), "full_name");
                // Ignore fork-originating PRs: head.ref alone is not a unique repository identity.
                // Do not let a fork impersonate the expected same-repository branch.
                if (destinationRepository != $"{owner}/{repo}")
                    throw new InvalidDataException("GitHub PR base repository mismatch.");
                if (sourceRepository != $"{owner}/{repo}")
                    continue;
                candidates.Add(new PortablePullRequestCandidate
                {
                    Number = number,
                    Repository = $"https://github.com/{owner}/{repo}",
                    HeadBranch = RequiredString(head, "ref"),
                    BaseBranch = RequiredString(baseRef, "ref"),
                    HeadCommit = RequiredString(head, "sha")
                });
            }
            if (items.Length < PageSize)
                return new PortablePullRequestSnapshot { QueryComplete = true, Candidates = candidates.ToArray() };
        }
        return new PortablePullRequestSnapshot { QueryComplete = false, Candidates = [] };
    }

    private static JsonElement RequiredObject(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var result) ||
            result.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Missing GitHub PR nested object.");
        return result;
    }
    private static string RequiredString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var result) ||
            result.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(result.GetString()))
            throw new InvalidDataException("Missing GitHub PR string property.");
        return result.GetString()!;
    }
    private static bool ValidSegment(string value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 80 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
