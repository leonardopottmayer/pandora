using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;

/// <summary>
/// Parses CSV files for all supported bank formats. The layout config drives all format-specific
/// behaviour: delimiter, date format, decimal separator, column mapping, sign convention, and
/// whether the file uses multi-section layout (Viacredi).
/// </summary>
internal sealed class CsvParser : IImportParser
{
    public bool CanParse(ImportLayout layout) => layout.IsCsv;

    public Task<IReadOnlyList<ParsedImportRow>> ParseAsync(
        byte[] fileBytes, ImportLayout layout, CancellationToken ct = default)
    {
        var cfg = JsonDocument.Parse(layout.Config).RootElement;
        var encodingName = GetString(cfg, "encoding", "UTF-8");
        var encoding = string.Equals(encodingName, "windows-1252", StringComparison.OrdinalIgnoreCase)
            ? Encoding.GetEncoding(1252)
            : Encoding.UTF8;

        var text = encoding.GetString(fileBytes);
        var delimiter = GetString(cfg, "delimiter", ",")[0];
        bool isMultiSection = GetBool(cfg, "isMultiSection");

        var allDataLines = isMultiSection
            ? ExtractMultiSectionRows(text, delimiter)
            : ExtractSimpleRows(text);

        if (allDataLines.Count == 0)
            return Task.FromResult<IReadOnlyList<ParsedImportRow>>([]);

        // First line of allDataLines is always the column header
        var headerLine = allDataLines[0];
        var headers = ParseCsvLine(headerLine, delimiter)
            .Select(h => h.Trim())
            .ToList();

        var dateCol = FindColumnIndex(headers, GetString(cfg, "dateColumn", "date"));
        var amountCol = FindColumnIndex(headers, GetString(cfg, "amountColumn", "amount"));
        var descCol = FindColumnIndex(headers, GetString(cfg, "descriptionColumn", "title"));
        var idCol = GetOptionalColumnIndex(headers, cfg, "identifierColumn");
        var signCol = GetOptionalColumnIndex(headers, cfg, "signColumn");

        var dateFormat = GetString(cfg, "dateFormat", "yyyy-MM-dd");
        var decimalSep = GetString(cfg, "amountDecimalSeparator", ".");
        bool amountAlwaysPositive = GetBool(cfg, "amountIsAlwaysPositive");
        bool positiveIsExpense = GetBool(cfg, "positiveAmountIsExpense");
        var creditSignValue = GetString(cfg, "creditSignValue", "");
        var debitSignValue = GetString(cfg, "debitSignValue", "");

        var installmentRegexes = BuildInstallmentPatterns(cfg);

        var rows = new List<ParsedImportRow>();
        int rowIndex = 0;

        for (int i = 1; i < allDataLines.Count; i++)
        {
            var line = allDataLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCsvLine(line, delimiter);
            if (fields.Count <= Math.Max(dateCol, Math.Max(amountCol, descCol)))
            {
                rowIndex++;
                continue;
            }

            var rawDate = fields[dateCol];
            var rawAmount = fields[amountCol];
            var description = fields[descCol].Trim();
            string? externalId = idCol >= 0 && idCol < fields.Count
                ? NullIfEmpty(fields[idCol])
                : null;

            // Normalize decimal separator
            if (decimalSep == ",")
                rawAmount = rawAmount.Replace(',', '.').Replace(" ", "");
            else
                rawAmount = rawAmount.Replace(",", "").Replace(" ", "");

            if (!decimal.TryParse(rawAmount, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                rowIndex++;
                continue;
            }

            if (!TryParseDate(rawDate, dateFormat, out var occurredOn))
            {
                rowIndex++;
                continue;
            }

            bool isCredit;
            bool shouldSkip = false;

            if (signCol >= 0 && signCol < fields.Count)
            {
                var signValue = fields[signCol].Trim();
                isCredit = string.Equals(signValue, creditSignValue, StringComparison.OrdinalIgnoreCase);
                amount = Math.Abs(amount);
            }
            else if (amountAlwaysPositive)
            {
                // For card imports: positive amount = expense (isCredit = false)
                isCredit = false;
                amount = Math.Abs(amount);
            }
            else if (positiveIsExpense)
            {
                // Itaú card: positive = expense, negative = payment/refund
                if (amount < 0)
                    isCredit = true; // negative on card = payment received = credit to balance
                else
                    isCredit = false; // positive on card = expense
                amount = Math.Abs(amount);
            }
            else
            {
                // Standard: positive = credit (incoming money), negative = debit (outgoing)
                isCredit = amount > 0;
                amount = Math.Abs(amount);
            }

            if (amount == 0m)
            {
                rowIndex++;
                continue;
            }

            var (installmentNumber, installmentCount, cleanDescription) =
                ExtractInstallment(description, installmentRegexes);

            rows.Add(new ParsedImportRow(
                RowIndex: rowIndex,
                RawData: line,
                Description: cleanDescription.Trim(),
                Payee: null,
                OccurredOn: occurredOn,
                Amount: amount,
                IsCredit: isCredit,
                Currency: "BRL",
                ExternalId: externalId,
                InstallmentNumber: installmentNumber,
                InstallmentCount: installmentCount,
                ShouldSkip: shouldSkip));

            rowIndex++;
        }

        return Task.FromResult<IReadOnlyList<ParsedImportRow>>(rows);
    }

    // ─── Row extraction ──────────────────────────────────────────────────────

    private static IReadOnlyList<string> ExtractSimpleRows(string text)
    {
        // For simple CSV: the entire file as lines (header + data rows)
        return text.ReplaceLineEndings("\n").Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    private static IReadOnlyList<string> ExtractMultiSectionRows(string text, char delimiter)
    {
        // Viacredi-style: multiple date-sections, each starting with a metadata line followed by
        // a column header line. We gather one header (from the first section) plus all data rows.
        var lines = text.ReplaceLineEndings("\n").Split('\n').ToList();
        var result = new List<string>();
        string? capturedHeader = null;
        bool nextIsHeader = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                nextIsHeader = false;
                continue;
            }

            // Lines starting with "Data do Extrato" mark the start of a section
            if (line.StartsWith("Data do Extrato", StringComparison.OrdinalIgnoreCase))
            {
                nextIsHeader = true;
                continue;
            }

            if (nextIsHeader)
            {
                // This is the column header line
                if (capturedHeader is null)
                {
                    capturedHeader = line;
                    result.Add(line); // Include header once as first element
                }
                nextIsHeader = false;
                continue;
            }

            // Skip the account-info block that precedes the first section
            if (capturedHeader is null) continue;

            result.Add(line);
        }

        return result;
    }

    // ─── CSV line parsing ────────────────────────────────────────────────────

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    // ─── Date parsing ────────────────────────────────────────────────────────

    private static bool TryParseDate(string raw, string format, out DateOnly date)
    {
        date = default;
        raw = raw.Trim();

        // Handle datetime formats: take date part only
        if (raw.Length > 10 && format.Contains("HH"))
        {
            var datePart = raw[..raw.IndexOf(' ')];
            return DateOnly.TryParseExact(datePart, format.Split(' ')[0], null,
                System.Globalization.DateTimeStyles.None, out date);
        }

        return DateOnly.TryParseExact(raw, format, null,
            System.Globalization.DateTimeStyles.None, out date);
    }

    // ─── Installment extraction ──────────────────────────────────────────────

    private static (short? Number, short? Count, string Clean) ExtractInstallment(
        string description, IReadOnlyList<Regex> patterns)
    {
        foreach (var pattern in patterns)
        {
            var m = pattern.Match(description);
            if (m.Success &&
                short.TryParse(m.Groups[1].Value, out var number) &&
                short.TryParse(m.Groups[2].Value, out var count) &&
                number >= 1 && count >= number)
            {
                var clean = (description[..m.Index].TrimEnd(' ', '-') +
                    description[(m.Index + m.Length)..]).Trim();
                return (number, count, clean);
            }
        }
        return (null, null, description);
    }

    private static IReadOnlyList<Regex> BuildInstallmentPatterns(JsonElement cfg)
    {
        if (!cfg.TryGetProperty("installmentPatterns", out var arr)) return [];
        var list = new List<Regex>();
        foreach (var el in arr.EnumerateArray())
        {
            if (el.GetString() is { } pattern)
                list.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        }
        return list;
    }

    // ─── Column helpers ──────────────────────────────────────────────────────

    private static int FindColumnIndex(List<string> headers, string name)
    {
        for (int i = 0; i < headers.Count; i++)
            if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        return 0;
    }

    private static int GetOptionalColumnIndex(List<string> headers, JsonElement cfg, string key)
    {
        if (!cfg.TryGetProperty(key, out var el) || el.ValueKind == JsonValueKind.Null) return -1;
        var name = el.GetString();
        if (string.IsNullOrEmpty(name)) return -1;
        return FindColumnIndex(headers, name);
    }

    // ─── Config helpers ──────────────────────────────────────────────────────

    private static bool GetBool(JsonElement cfg, string key)
        => cfg.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.True;

    private static string GetString(JsonElement cfg, string key, string def)
        => cfg.TryGetProperty(key, out var el) && el.GetString() is { } s ? s : def;

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
