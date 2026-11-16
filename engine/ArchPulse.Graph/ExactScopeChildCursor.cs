// SPDX-License-Identifier: Apache-2.0
using System.Security.Cryptography;
using System.Text;

namespace ArchPulse.Graph;

/// <summary>
/// Revision-and-scope-bound cursor for trusted local canonical child pages.
/// A digest detects accidental cross-query reuse; it is NOT an authenticated
/// or tamperproof cursor and must not be exposed as a permission boundary.
/// </summary>
public static class ExactScopeChildCursor
{
    public const string Schema = "archpulse.local-child-cursor/v1";

    public static string Create(string project, string revision, string parentSymbolId,
        int pageSize, string lastChildSymbolId)
    {
        Validate(project, revision, parentSymbolId, pageSize, lastChildSymbolId);
        string payload = string.Join("\n", Schema, project, revision, parentSymbolId,
            pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            lastChildSymbolId);
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
            + "." + hash;
    }

    public static string Read(string cursor, string project, string revision,
        string parentSymbolId, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 12000)
            throw new ArgumentException("Invalid bounded child cursor.", nameof(cursor));
        int dot = cursor.LastIndexOf('.');
        if (dot is <= 0 || dot == cursor.Length - 1)
            throw new ArgumentException("Invalid child cursor encoding.", nameof(cursor));
        string encoded = cursor[..dot];
        if (encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("Invalid child cursor encoding.", nameof(cursor));
        string base64 = encoded.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException ex) { throw new ArgumentException("Invalid child cursor encoding.", nameof(cursor), ex); }
        if (bytes.Length > 8192)
            throw new ArgumentException("Child cursor exceeds local budget.", nameof(cursor));
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        byte[] expected = Encoding.ASCII.GetBytes(hash);
        byte[] received = Encoding.ASCII.GetBytes(cursor[(dot + 1)..]);
        if (!CryptographicOperations.FixedTimeEquals(expected, received))
            throw new ArgumentException("Child cursor checksum mismatch.", nameof(cursor));
        string[] values = Encoding.UTF8.GetString(bytes).Split('\n');
        if (values.Length != 6 || values[0] != Schema || values[1] != project ||
            values[2] != revision || values[3] != parentSymbolId ||
            values[4] != pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw new ArgumentException("Cursor does not match exact scope and revision.", nameof(cursor));
        Validate(project, revision, parentSymbolId, pageSize, values[5]);
        if (Create(project, revision, parentSymbolId, pageSize, values[5]) != cursor)
            throw new ArgumentException("Noncanonical child cursor encoding.", nameof(cursor));
        return values[5];
    }

    private static void Validate(string project, string revision, string parent,
        int pageSize, string child)
    {
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        foreach (var value in new[] { project, revision, parent, child })
            if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 ||
                value.Contains('\n') || value.Contains('\r'))
                throw new ArgumentException("Cursor field is not bounded or canonical.");
    }
}
