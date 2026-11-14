// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchPulse.Graph;

/// <summary>
/// A local operator assertion, NOT an authenticated identity, business proof,
/// EvidenceKind.Verified, or consent to change the canonical source graph.
/// Automated proposals have no write path into this human review journal.
/// </summary>
public enum FeatureReviewAction { Unspecified, ApproveCandidate, RejectCandidate, Rollback }
public enum FeatureReviewDisposition { Unreviewed, OperatorApprovedCandidate, OperatorRejectedCandidate }
public enum FeatureReviewActorKind { Unspecified, LocalHumanDeclared, Automation }

public sealed record FeatureReviewRequest(
    FeatureReviewAction Action, FeatureReviewActorKind ActorKind,
    string Actor, string Reason, string ExpectedHead,
    string? FeatureId = null, string? SymbolId = null, int? RollbackToSequence = null);

public sealed record FeatureReviewReceipt(
    string Schema, string Project, string GraphRevision, string CatalogSha256,
    int Sequence, string PreviousHash, string Hash,
    FeatureReviewAction Action, FeatureReviewActorKind ActorKind,
    string Actor, string Reason, string? FeatureId, string? SymbolId,
    int? RollbackToSequence, DateTimeOffset RecordedAtUtc);

public sealed record FeatureReviewMemberState(
    string FeatureId, string SymbolId, FeatureReviewDisposition Disposition,
    int LastReviewedSequence);

public sealed record FeatureReviewSummary(
    string Schema, string Project, string GraphRevision, string CatalogSha256,
    string Scope, string Head, int Sequence,
    IReadOnlyList<FeatureReviewMemberState> Members,
    IReadOnlyList<FeatureReviewReceipt> History);

/// <summary>
/// Atomic, append-only receipt sequence on one trusted local volume. Each entry
/// is SHA256 chained and bound to the immutable source graph + catalog digest.
/// A rollback is a NEW explicit human receipt, not deletion of prior decisions.
/// No remote endpoint can append. This is not a multi-tenant authenticated ledger.
/// </summary>
public sealed class FeatureReviewJournal
{
    public const string Schema = "archpulse.feature-reviews/v1";
    public const string Scope = "LocalHumanDeclared;NotAuthenticated;NotBusinessVerified";
    public const string Genesis = "GENESIS";
    private const int MaxReceipts = 2_000;
    private const int MaxReceiptBytes = 16 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private readonly string _root;
    public FeatureReviewJournal(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A trusted local journal directory is required.");
        _root = Path.GetFullPath(directory);
    }

    public FeatureReviewSummary Read(GraphSnapshot graph, FeatureCatalog catalog)
    {
        FeatureAtlas.Validate(graph, catalog);
        string digest = CatalogDigest(catalog);
        string dir = PathFor(graph, digest);
        var members = catalog.Features.SelectMany(f => f.Members.Select(m =>
                new FeatureReviewMemberState(f.Id, m.SymbolId,
                    FeatureReviewDisposition.Unreviewed, 0)))
            .OrderBy(x => x.FeatureId, StringComparer.Ordinal)
            .ThenBy(x => x.SymbolId, StringComparer.Ordinal).ToArray();
        var initial = members.ToDictionary(x => (x.FeatureId, x.SymbolId));
        var current = new Dictionary<(string, string), FeatureReviewMemberState>(initial);
        var historicalStates = new List<Dictionary<(string, string), FeatureReviewMemberState>>
        {
            new(initial)
        };
        var events = new List<FeatureReviewReceipt>();
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.review-receipt.json").Order(StringComparer.Ordinal).ToArray()
            : [];
        if (files.Length > MaxReceipts)
            throw new InvalidDataException("Feature review history exceeds the bounded journal.");
        string previousHash = Genesis;
        for (int i = 0; i < files.Length; i++)
        {
            var expectedFile = Path.Combine(dir, FileName(i + 1));
            if (!string.Equals(files[i], expectedFile, StringComparison.Ordinal))
                throw new InvalidDataException("Review sequence is missing, duplicated or malformed.");
            var info = new FileInfo(files[i]);
            if (info.Length is < 1 or > MaxReceiptBytes)
                throw new InvalidDataException("Review receipt is missing or exceeds the byte limit.");
            var data = File.ReadAllBytes(files[i]);
            if (data.Length > MaxReceiptBytes)
                throw new InvalidDataException("Review receipt exceeded its byte limit.");
            var receipt = JsonSerializer.Deserialize<FeatureReviewReceipt>(data, Options)
                ?? throw new InvalidDataException("Review receipt is empty.");
            // The hash binds the serialized model; reject uncommitted or duplicate
            // JSON fields which a last-value-wins model reader would otherwise lose.
            // Append always writes the same canonical UTF-8 representation.
            if (!data.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(receipt, Options)))
                throw new InvalidDataException("Review receipt JSON is not canonical.");
            if (receipt.Schema != Schema || receipt.Project != graph.Project ||
                receipt.GraphRevision != graph.Revision || receipt.CatalogSha256 != digest ||
                receipt.Sequence != i + 1 || receipt.PreviousHash != previousHash ||
                receipt.Hash != ComputeHash(receipt with { Hash = "" }))
                throw new InvalidDataException("Review receipt hash chain or identity is invalid.");
            ValidateRequest(new FeatureReviewRequest(receipt.Action, receipt.ActorKind,
                receipt.Actor, receipt.Reason, receipt.PreviousHash, receipt.FeatureId,
                receipt.SymbolId, receipt.RollbackToSequence), current, historicalStates.Count - 1);
            current = Apply(receipt, current, historicalStates);
            historicalStates.Add(new(current));
            events.Add(receipt);
            previousHash = receipt.Hash;
        }
        return new FeatureReviewSummary(Schema, graph.Project, graph.Revision, digest,
            Scope, previousHash, events.Count,
            current.Values.OrderBy(x => x.FeatureId, StringComparer.Ordinal)
                .ThenBy(x => x.SymbolId, StringComparer.Ordinal).ToArray(), events);
    }

    public FeatureReviewSummary Append(GraphSnapshot graph, FeatureCatalog catalog,
        FeatureReviewRequest request)
    {
        var summary = Read(graph, catalog);
        if (request is null || request.ExpectedHead != summary.Head)
            throw new InvalidOperationException("Review head changed; reload history and retry explicitly.");
        var current = summary.Members.ToDictionary(x => (x.FeatureId, x.SymbolId));
        ValidateRequest(request, current, summary.Sequence);
        if (summary.Sequence >= MaxReceipts)
            throw new InvalidDataException("Review history cap reached.");
        var receipt = new FeatureReviewReceipt(Schema, graph.Project, graph.Revision,
            summary.CatalogSha256, summary.Sequence + 1, summary.Head, "",
            request.Action, request.ActorKind, request.Actor, request.Reason,
            request.FeatureId, request.SymbolId, request.RollbackToSequence,
            DateTimeOffset.UtcNow);
        receipt = receipt with { Hash = ComputeHash(receipt) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(receipt, Options);
        if (bytes.Length > MaxReceiptBytes)
            throw new InvalidDataException("Review receipt is too large.");
        var directory = PathFor(graph, summary.CatalogSha256);
        Directory.CreateDirectory(directory);
        string dest = Path.Combine(directory, FileName(receipt.Sequence));
        string temp = Path.Combine(directory, ".receipt-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            try { File.Move(temp, dest, overwrite: false); }
            catch (IOException) when (File.Exists(dest))
            {
                throw new InvalidOperationException("Concurrent human review conflict: reload current journal head.");
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return Read(graph, catalog);
    }

    public static FeatureReviewRequest ParseRequest(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > 16_384)
            throw new InvalidDataException("Review instruction must be nonempty and bounded.");
        using var document = JsonDocument.Parse(text);
        RejectDuplicateProperties(document.RootElement);
        return document.RootElement.Deserialize<FeatureReviewRequest>(Options)
            ?? throw new InvalidDataException("Review instruction is empty.");
    }

    // Web JSON property names are matched case-insensitively. Neither an exact
    // duplicate nor a differently cased alias may override local operator intent.
    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                    throw new InvalidDataException("Review instruction has duplicate JSON properties.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                RejectDuplicateProperties(item);
        }
    }

    private static void ValidateRequest(FeatureReviewRequest request,
        Dictionary<(string, string), FeatureReviewMemberState> state, int previousSequence)
    {
        if (request.ActorKind != FeatureReviewActorKind.LocalHumanDeclared ||
            request.Action is not (FeatureReviewAction.ApproveCandidate or
                                   FeatureReviewAction.RejectCandidate or
                                   FeatureReviewAction.Rollback) ||
            string.IsNullOrWhiteSpace(request.Actor) || request.Actor.Length > 160 ||
            request.Actor.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1024 ||
            request.Reason.Any(char.IsControl))
            throw new InvalidDataException("Only a bounded explicit local human-declared review is accepted.");
        if (request.Action == FeatureReviewAction.Rollback)
        {
            if (request.FeatureId is not null || request.SymbolId is not null ||
                request.RollbackToSequence is null ||
                request.RollbackToSequence < 0 || request.RollbackToSequence >= previousSequence)
                throw new InvalidDataException("Rollback requires an older existing review sequence, not a future one.");
        }
        else
        {
            if (request.RollbackToSequence is not null ||
                request.FeatureId is null || request.SymbolId is null ||
                !state.ContainsKey((request.FeatureId, request.SymbolId)))
                throw new InvalidDataException("Review must refer to an existing exact candidate member.");
        }
    }

    private static Dictionary<(string, string), FeatureReviewMemberState> Apply(
        FeatureReviewReceipt receipt,
        Dictionary<(string, string), FeatureReviewMemberState> state,
        IReadOnlyList<Dictionary<(string, string), FeatureReviewMemberState>> historical)
    {
        if (receipt.Action == FeatureReviewAction.Rollback)
            return new(historical[receipt.RollbackToSequence!.Value]);
        var next = new Dictionary<(string, string), FeatureReviewMemberState>(state);
        var key = (receipt.FeatureId!, receipt.SymbolId!);
        next[key] = next[key] with
        {
            Disposition = receipt.Action == FeatureReviewAction.ApproveCandidate
                ? FeatureReviewDisposition.OperatorApprovedCandidate
                : FeatureReviewDisposition.OperatorRejectedCandidate,
            LastReviewedSequence = receipt.Sequence
        };
        return next;
    }

    private string PathFor(GraphSnapshot graph, string digest)
    {
        var key = Encoding.UTF8.GetBytes("feature-review-v1\0" + graph.Project + "\0" +
                                         graph.Revision + "\0" + digest);
        return Path.Combine(_root, Convert.ToHexStringLower(SHA256.HashData(key)) + ".feature-review");
    }

    private static string CatalogDigest(FeatureCatalog catalog) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(catalog, Options)));

    private static string FileName(int sequence) => $"{sequence:D6}.review-receipt.json";
    private static string ComputeHash(FeatureReviewReceipt receipt) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(receipt, Options)));
}
