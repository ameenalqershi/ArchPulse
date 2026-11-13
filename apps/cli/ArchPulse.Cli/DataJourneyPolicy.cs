// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchPulse.Graph;

internal enum DataClassification { Public, Internal, Confidential, Restricted }
internal enum ReviewDisposition { AllowedByPolicy, RequiresReview, BlockedByPolicy }
internal sealed record ZoneGrant(string From, string To, DataClassification Classification);

internal sealed class DataJourneyPolicy
{
    private const int MaxPolicyBytes = 128 * 1024;
    public required Dictionary<string, DataClassification> SourceMethods { get; init; }
    public required Dictionary<string, string> ProjectZones { get; init; }
    public required ZoneGrant[] Grants { get; init; }

    public static DataJourneyPolicy Load(string path)
    {
        using var input = File.OpenRead(path);
        if (input.Length is < 1 or > MaxPolicyBytes)
            throw new InvalidDataException("DataJourney policy exceeds its input limit or is empty.");

        var data = new byte[(int)input.Length];
        input.ReadExactly(data);
        if (input.ReadByte() != -1)
            throw new InvalidDataException("DataJourney policy changed during the read.");

        try
        {
            using var parsed = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 12 });
            RejectDuplicateProperties(parsed.RootElement);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                Converters = { new JsonStringEnumConverter<DataClassification>(allowIntegerValues: false) }
            };
            return Validate(parsed.Deserialize<DataJourneyPolicy>(options));
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Malformed DataJourney policy. Check the policy schema.");
        }
    }

    private static DataJourneyPolicy Validate(DataJourneyPolicy? policy)
    {
        if (policy is null || policy.SourceMethods is null || policy.ProjectZones is null ||
            policy.Grants is null || policy.SourceMethods.Count > 512 || policy.ProjectZones.Count > 512 ||
            policy.Grants.Length > 512 ||
            policy.SourceMethods.Any(pair => !ValidText(pair.Key, 800) || !Enum.IsDefined(pair.Value)) ||
            policy.ProjectZones.Any(pair => !ValidText(pair.Key, 160) || !ValidText(pair.Value, 80)))
            throw new InvalidDataException("DataJourney policy has an invalid source or project zone.");

        var zones = policy.ProjectZones.Values.ToHashSet(StringComparer.Ordinal);
        var grants = new HashSet<ZoneGrant>();
        foreach (var grant in policy.Grants)
            if (grant is null || !zones.Contains(grant.From) || !zones.Contains(grant.To) ||
                grant.From == grant.To || !Enum.IsDefined(grant.Classification) || !grants.Add(grant))
                throw new InvalidDataException("DataJourney policy has an invalid or duplicate zone grant.");
        return policy;
    }

    private static bool ValidText(string value, int cap) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= cap && !value.Any(char.IsControl);

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException("DataJourney policy contains duplicate properties.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
                RejectDuplicateProperties(element);
        }
    }
}

internal sealed record ReviewedHop(DataJourneyHop Hop, string From, string To,
    string? FromZone, string? ToZone, ReviewDisposition Disposition);

internal sealed record ReviewedFlow(CrossProjectDataFlow Flow, DataClassification? Classification,
    ReviewDisposition Disposition, IReadOnlyList<ReviewedHop> Hops);

internal sealed class DataJourneyPolicyEvaluator(DataJourneyPolicy policy)
{
    private readonly HashSet<ZoneGrant> _grants = policy.Grants.ToHashSet();

    public ReviewedFlow Evaluate(CrossProjectDataFlow flow)
    {
        DataClassification? classification = policy.SourceMethods.TryGetValue(
            flow.SourceMethodSymbolId, out var configured) ? configured : null;
        var hops = flow.Hops.Select(hop => EvaluateHop(hop, classification)).ToArray();

        var disposition = ReviewDisposition.RequiresReview;
        if (hops.Any(hop => hop.Disposition == ReviewDisposition.BlockedByPolicy))
            disposition = ReviewDisposition.BlockedByPolicy;
        else if (flow.Boundary == DataJourneyBoundary.AnnotatedSink && classification is not null &&
                 hops.Length > 0 && hops.All(hop => hop.Disposition == ReviewDisposition.AllowedByPolicy))
            disposition = ReviewDisposition.AllowedByPolicy;
        return new(flow, classification, disposition, hops);
    }

    private ReviewedHop EvaluateHop(DataJourneyHop hop, DataClassification? classification)
    {
        string from = hop.From;
        string to = hop.To;
        string? fromZone = GetZone(from);
        string? toZone = GetZone(to);
        if (fromZone is null || toZone is null || classification is null)
            return new(hop, from, to, fromZone, toZone, ReviewDisposition.RequiresReview);
        return new(hop, from, to, fromZone, toZone,
            fromZone == toZone || _grants.Contains(new(fromZone, toZone, classification.Value))
                ? ReviewDisposition.AllowedByPolicy : ReviewDisposition.BlockedByPolicy);
    }

    private string? GetZone(string method)
    {
        int end = method.IndexOf("::M:", StringComparison.Ordinal);
        if (end <= 0 || method.IndexOf("::M:", end + 4, StringComparison.Ordinal) >= 0)
            return null;
        return policy.ProjectZones.GetValueOrDefault(method[..end]);
    }
}
