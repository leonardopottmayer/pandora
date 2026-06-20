using System.Text;
using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;

/// <summary>
/// Identifies the layout of an uploaded file by inspecting its content.
/// Detection is purely content-based; no user configuration is required.
///
/// OFX identification order:
///   1. CREDITCARDMSGSRSV1 + FID=260 → nubank-card-ofx
///   2. &lt;BANKINFO&gt; presence         → viacredi-ofx
///   3. FID=260 (BANKMSGSRSV1)       → nubank-account-ofx
///   4. FID=077 / ORG contains "Intermedium" → inter-ofx
///   5. BANKID=0341                  → itau-account-ofx
///
/// CSV identification (after header normalisation):
///   Semicolons present + "Conta;" prefix → viacredi-account-csv
///   Header = "date,title,amount"         → nubank-card-csv
///   Header contains "identificador"      → nubank-account-csv
///   Header = "data,lançamento,valor"     → itau-card-csv
/// </summary>
internal sealed class LayoutDetector : ILayoutDetector
{
    private static readonly Regex _fidRegex =
        new(@"<FID>(\d+)</?FID>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _orgRegex =
        new(@"<ORG>([^<]+)</?ORG>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _bankIdRegex =
        new(@"<BANKID>(\d+)</?BANKID>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<Result<ImportLayout>> DetectAsync(
        byte[] fileBytes, string fileName, IReadOnlyList<ImportLayout> systemLayouts,
        CancellationToken ct = default)
    {
        var text = ReadHeader(fileBytes, 2048);

        if (IsOfx(text))
        {
            var code = DetectOfxLayout(text);
            if (code is null)
                return Task.FromResult(Result<ImportLayout>.Failure(
                    [Domain.Errors.ImportErrors.LayoutNotDetected]));

            var layout = systemLayouts.FirstOrDefault(l => l.LayoutCode == code);
            return layout is not null
                ? Task.FromResult(Result<ImportLayout>.Success(layout))
                : Task.FromResult(Result<ImportLayout>.Failure(
                    [Domain.Errors.ImportErrors.LayoutNotDetected]));
        }

        if (IsCsv(text, fileName))
        {
            var fullText = ReadFull(fileBytes);
            var code = DetectCsvLayout(fullText);
            if (code is null)
                return Task.FromResult(Result<ImportLayout>.Failure(
                    [Domain.Errors.ImportErrors.LayoutNotDetected]));

            var layout = systemLayouts.FirstOrDefault(l => l.LayoutCode == code);
            return layout is not null
                ? Task.FromResult(Result<ImportLayout>.Success(layout))
                : Task.FromResult(Result<ImportLayout>.Failure(
                    [Domain.Errors.ImportErrors.LayoutNotDetected]));
        }

        return Task.FromResult(Result<ImportLayout>.Failure(
            [Domain.Errors.ImportErrors.LayoutNotDetected]));
    }

    // ─── Format detection ────────────────────────────────────────────────────

    private static bool IsOfx(string header)
        => header.Contains("OFXHEADER:", StringComparison.OrdinalIgnoreCase)
        || header.Contains("<OFX>", StringComparison.OrdinalIgnoreCase);

    private static bool IsCsv(string header, string fileName)
        => fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
        || (!header.Contains("<OFX>", StringComparison.OrdinalIgnoreCase)
            && (header.Contains(',') || header.Contains(';')));

    // ─── OFX layout identification ───────────────────────────────────────────

    private static string? DetectOfxLayout(string text)
    {
        bool isCard = text.Contains("CREDITCARDMSGSRSV1", StringComparison.OrdinalIgnoreCase);
        var fid = _fidRegex.Match(text).Groups[1].Value;
        var org = _orgRegex.Match(text).Groups[1].Value;
        var bankId = _bankIdRegex.Match(text).Groups[1].Value;

        if (isCard)
        {
            if (fid == "260" || org.Contains("NU PAGAMENTOS", StringComparison.OrdinalIgnoreCase))
                return "nubank-card-ofx";
            return null; // unknown card layout
        }

        // Account layouts
        if (text.Contains("<BANKINFO>", StringComparison.OrdinalIgnoreCase))
            return "viacredi-ofx";

        if (fid == "260" || org.Contains("NU PAGAMENTOS", StringComparison.OrdinalIgnoreCase))
            return "nubank-account-ofx";

        if (fid == "077" || org.Contains("Intermedium", StringComparison.OrdinalIgnoreCase))
            return "inter-ofx";

        if (bankId == "0341" || fid == "0341")
            return "itau-account-ofx";

        return null;
    }

    // ─── CSV layout identification ───────────────────────────────────────────

    private static string? DetectCsvLayout(string text)
    {
        var firstLine = text.Split('\n', 2)[0].Trim();
        var normalizedFirst = firstLine.ToLowerInvariant();

        // Viacredi: starts with "Conta;" (account number)
        if (normalizedFirst.StartsWith("conta;", StringComparison.OrdinalIgnoreCase))
            return "viacredi-account-csv";

        // Find the actual header row (for Viacredi multi-section, skip to "Data do Extrato" section;
        // but for others, the first line IS the header)
        var header = FindCsvHeader(text);
        if (header is null) return null;
        var normalizedHeader = header.ToLowerInvariant();

        // Nubank card: exactly "date,title,amount"
        if (normalizedHeader == "date,title,amount")
            return "nubank-card-csv";

        // Nubank account: contains "identificador"
        if (normalizedHeader.Contains("identificador"))
            return "nubank-account-csv";

        // Itaú card: "data,lançamento,valor" (with or without diacritics)
        if (normalizedHeader.Contains("lan") && normalizedHeader.Contains("valor")
            && normalizedHeader.Contains("data"))
            return "itau-card-csv";

        return null;
    }

    private static string? FindCsvHeader(string text)
    {
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            // Skip known meta-lines (Viacredi account info block)
            if (trimmed.StartsWith("Conta;", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("Titulares;", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("LEONARDO", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("Saldo;", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("Data do Extrato;", StringComparison.OrdinalIgnoreCase)) continue;
            return trimmed;
        }
        return null;
    }

    // ─── IO helpers ──────────────────────────────────────────────────────────

    private static string ReadHeader(byte[] bytes, int maxBytes)
    {
        var slice = bytes[..Math.Min(maxBytes, bytes.Length)];
        try { return Encoding.UTF8.GetString(slice); }
        catch { return Encoding.GetEncoding(1252).GetString(slice); }
    }

    private static string ReadFull(byte[] bytes)
    {
        try { return Encoding.UTF8.GetString(bytes); }
        catch { return Encoding.GetEncoding(1252).GetString(bytes); }
    }
}
