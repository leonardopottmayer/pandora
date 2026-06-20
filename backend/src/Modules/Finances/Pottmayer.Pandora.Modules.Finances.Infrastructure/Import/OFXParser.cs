using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;

/// <summary>
/// Parses OFX 1.x SGML files. Handles all known bank quirks via the layout config:
/// - no-closing-tags (Itaú): pre-processes the SGML to add missing closing tags
/// - multiple-banktranlist (Viacredi): collects transactions from all BANKTRANLIST blocks
/// - comma-decimal (Viacredi): treats comma as the decimal separator in TRNAMT
/// - invertAmount: negates the parsed amount (Nubank card — purchases arrive as negative)
/// - amountIsAlwaysAbsolute: uses TRNTYPE to infer sign (Viacredi)
/// - treatPaymentAsDebit: treats TRNTYPE=PAYMENT as a debit (Inter)
/// - fitid-shared-with-secondary: adds description to the dedup key to distinguish IOF rows
/// </summary>
internal sealed class OFXParser : IImportParser
{
    private static readonly Regex _dateTimeRegex =
        new(@"^(\d{8})(\d{6})?(\[.*\])?$", RegexOptions.Compiled);

    private static readonly Regex[] _installmentPatterns =
    [
        new(@"-\s*Parcela\s+(\d+)/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(\d+)/(\d+)\b", RegexOptions.Compiled),
    ];

    public bool CanParse(ImportLayout layout) => layout.IsOfx;

    public Task<IReadOnlyList<ParsedImportRow>> ParseAsync(
        byte[] fileBytes, ImportLayout layout, CancellationToken ct = default)
    {
        var config = JsonDocument.Parse(layout.Config).RootElement;
        var quirks = GetQuirks(config);

        var encoding = DetectEncoding(fileBytes);
        var text = encoding.GetString(fileBytes);

        var sgml = ExtractSgmlBody(text);
        // OFX 1.x SGML omits closing tags on leaf elements; normalise them so tag extraction works
        // for every layout, not only those flagged with the "no-closing-tags" quirk. AddClosingTags
        // is idempotent — it copies through values that already have a closing tag.
        sgml = AddClosingTags(sgml);

        var rawTransactions = ExtractAllTransactions(sgml);

        bool invertAmount = GetBool(config, "invertAmount");
        bool amountIsAbsolute = GetBool(config, "amountIsAlwaysAbsolute");
        bool treatPaymentAsDebit = GetBool(config, "treatPaymentAsDebit");
        bool fitidShared = quirks.Contains("fitid-shared-with-secondary");
        bool commaDecimal = quirks.Contains("comma-decimal");
        string descField = GetString(config, "descriptionField", "MEMO");

        var rows = new List<ParsedImportRow>();
        int rowIndex = 0;

        foreach (var trn in rawTransactions)
        {
            var trnType = GetTag(trn, "TRNTYPE");
            var rawAmount = GetTag(trn, "TRNAMT");
            var fitid = GetTag(trn, "FITID");
            var dtPosted = GetTag(trn, "DTPOSTED");
            var memo = GetTag(trn, descField) ?? GetTag(trn, "MEMO") ?? GetTag(trn, "NAME") ?? string.Empty;

            if (commaDecimal)
                rawAmount = rawAmount?.Replace(',', '.');

            if (!decimal.TryParse(rawAmount, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                rowIndex++;
                continue;
            }

            if (!TryParseOfxDate(dtPosted, out var occurredOn))
            {
                rowIndex++;
                continue;
            }

            bool isCredit;
            if (amountIsAbsolute)
            {
                isCredit = string.Equals(trnType, "CREDIT", StringComparison.OrdinalIgnoreCase);
                amount = Math.Abs(amount);
            }
            else if (invertAmount)
            {
                amount = -amount;
                isCredit = amount > 0;
                amount = Math.Abs(amount);
            }
            else
            {
                isCredit = amount > 0;
                if (treatPaymentAsDebit && string.Equals(trnType, "PAYMENT", StringComparison.OrdinalIgnoreCase))
                    isCredit = false;
                amount = Math.Abs(amount);
            }

            // Skip zero-amount system rows (e.g., Viacredi "SALDO ANTERIOR")
            if (amount == 0m)
            {
                rowIndex++;
                continue;
            }

            var (installmentNumber, installmentCount, cleanDescription) = ExtractInstallment(memo);

            // For layouts where FITID is shared between main tx and secondary (IOF), suffix with description
            string? externalId = string.IsNullOrWhiteSpace(fitid) ? null : fitid;
            if (fitidShared && externalId is not null)
                externalId = $"{fitid}:{NormalizeForDedup(memo)}";

            rows.Add(new ParsedImportRow(
                RowIndex: rowIndex,
                RawData: trn,
                Description: cleanDescription.Trim(),
                Payee: null,
                OccurredOn: occurredOn,
                Amount: amount,
                IsCredit: isCredit,
                Currency: "BRL",
                ExternalId: externalId,
                InstallmentNumber: installmentNumber,
                InstallmentCount: installmentCount));

            rowIndex++;
        }

        return Task.FromResult<IReadOnlyList<ParsedImportRow>>(rows);
    }

    // ─── SGML helpers ────────────────────────────────────────────────────────

    private static string ExtractSgmlBody(string text)
    {
        var ofxStart = text.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        return ofxStart >= 0 ? text[ofxStart..] : text;
    }

    private static string AddClosingTags(string sgml)
    {
        // Adds </TAG> after each value where the closing tag is missing.
        // Matches: <TAG>value followed by something that is not </TAG>
        var result = new StringBuilder();
        // Exclude \r as well as \n: with CRLF line endings a container tag (e.g. <STMTTRN>) is
        // immediately followed by \r\n, and a value class that allowed \r would capture a phantom
        // value and wrongly self-close the container.
        var tagRegex = new Regex(@"<([A-Z0-9.]+)>([^<>\r\n]+?)(?=\s*<)", RegexOptions.Singleline);

        int lastPos = 0;
        foreach (Match m in tagRegex.Matches(sgml))
        {
            result.Append(sgml, lastPos, m.Index - lastPos);
            var tagName = m.Groups[1].Value;
            var value = m.Groups[2].Value.TrimEnd();
            // If a closing tag follows, just copy through; otherwise inject it
            var afterValue = sgml[(m.Index + m.Length)..].TrimStart();
            if (afterValue.StartsWith($"</{tagName}>", StringComparison.OrdinalIgnoreCase))
                result.Append(m.Value);
            else
                result.Append($"<{tagName}>{value}</{tagName}>");
            lastPos = m.Index + m.Length;
        }
        result.Append(sgml, lastPos, sgml.Length - lastPos);
        return result.ToString();
    }

    private static IReadOnlyList<string> ExtractAllTransactions(string sgml)
    {
        var result = new List<string>();
        var stmtTrnRegex = new Regex(
            @"<STMTTRN>(.*?)</STMTTRN>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match m in stmtTrnRegex.Matches(sgml))
            result.Add(m.Value);

        return result;
    }

    private static string? GetTag(string block, string tag)
    {
        var m = Regex.Match(block,
            $@"<{tag}>(.*?)</{tag}>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    // ─── Date parsing ────────────────────────────────────────────────────────

    private static bool TryParseOfxDate(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var m = _dateTimeRegex.Match(raw.Trim());
        if (!m.Success) return false;

        var dateStr = m.Groups[1].Value; // yyyyMMdd
        if (!DateOnly.TryParseExact(dateStr, "yyyyMMdd", null,
            System.Globalization.DateTimeStyles.None, out date)) return false;

        return true;
    }

    // ─── Encoding detection ──────────────────────────────────────────────────

    private static Encoding DetectEncoding(byte[] bytes)
    {
        // Check for UTF-8 BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;

        // Scan the OFX header for ENCODING/CHARSET lines
        var header = Encoding.ASCII.GetString(bytes[..Math.Min(512, bytes.Length)]);
        if (header.Contains("ENCODING:UTF-8", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8;
        if (header.Contains("CHARSET:1252", StringComparison.OrdinalIgnoreCase))
            return Encoding.GetEncoding(1252);

        return Encoding.UTF8;
    }

    // ─── Installment extraction ──────────────────────────────────────────────

    private static (short? Number, short? Count, string Clean) ExtractInstallment(string description)
    {
        foreach (var pattern in _installmentPatterns)
        {
            var m = pattern.Match(description);
            if (m.Success &&
                short.TryParse(m.Groups[1].Value, out var number) &&
                short.TryParse(m.Groups[2].Value, out var count) &&
                number >= 1 && count >= number)
            {
                var clean = description[..m.Index].TrimEnd(' ', '-').Trim();
                return (number, count, clean);
            }
        }
        return (null, null, description);
    }

    internal static string NormalizeForDedup(string description)
        => Regex.Replace(description.ToLowerInvariant().Trim(), @"\s+", " ");

    // ─── Config helpers ──────────────────────────────────────────────────────

    private static HashSet<string> GetQuirks(JsonElement config)
    {
        if (!config.TryGetProperty("quirks", out var quirksEl)) return [];
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in quirksEl.EnumerateArray())
            if (q.GetString() is { } s) set.Add(s);
        return set;
    }

    private static bool GetBool(JsonElement config, string key)
        => config.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.True;

    private static string GetString(JsonElement config, string key, string defaultValue)
        => config.TryGetProperty(key, out var el) && el.GetString() is { } s ? s : defaultValue;
}
