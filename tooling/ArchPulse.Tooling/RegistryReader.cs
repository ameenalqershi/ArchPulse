// SPDX-License-Identifier: Apache-2.0
using Microsoft.VisualBasic.FileIO;

namespace ArchPulse.Tooling;

public static class RegistryReader
{
    public static IReadOnlyList<Dictionary<string, string>> Read(
        string file, IReadOnlyList<string> headers, ICollection<string> errors)
    {
        try
        {
            using var parser = new TextFieldParser(file);
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;
            var actual = parser.ReadFields() ?? [];
            if (!actual.SequenceEqual(headers, StringComparer.Ordinal))
            {
                errors.Add($"{Path.GetFileName(file)}: CSV header differs from documented schema");
                return [];
            }
            var rows = new List<Dictionary<string, string>>();
            var line = 1;
            while (!parser.EndOfData)
            {
                line++;
                var cells = parser.ReadFields() ?? [];
                if (cells.Length != headers.Count || cells.Any(string.IsNullOrWhiteSpace))
                {
                    errors.Add($"{Path.GetFileName(file)}:{line}: missing or malformed field");
                    continue;
                }
                rows.Add(headers.Select((header, i) => (header, value: cells[i]))
                    .ToDictionary(x => x.header, x => x.value, StringComparer.Ordinal));
            }
            return rows;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or MalformedLineException)
        {
            errors.Add($"{Path.GetFileName(file)}: unreadable CSV: {ex.Message}");
            return [];
        }
    }
}
