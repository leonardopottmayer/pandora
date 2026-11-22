using System.Text;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;

/// <summary>
/// Resolves the layout deterministically from the destination's bank (COMPE code) + file format +
/// account/card type. When the destination has no bank, or no layout matches that combination, it
/// falls back to the content-sniffing <see cref="ILayoutDetector"/> so imports keep working.
/// </summary>
internal sealed class ImportLayoutResolver(ILayoutDetector detector) : IImportLayoutResolver
{
    public Task<Result<ImportLayout>> ResolveAsync(
        byte[] fileBytes, string fileName, string? bankCode, bool isCard,
        IReadOnlyList<ImportLayout> systemLayouts, CancellationToken ct = default)
    {
        var format = DetectFormat(fileBytes, fileName);

        if (!string.IsNullOrWhiteSpace(bankCode) && format is not null)
        {
            var accountType = isCard ? ImportLayoutAccountType.Card : ImportLayoutAccountType.Account;

            var match = systemLayouts.FirstOrDefault(l =>
                Bank.Matches(l.BankCode, bankCode) &&
                l.FileFormat.Value == format &&
                l.AccountType == accountType);

            if (match is not null)
                return Task.FromResult(Result<ImportLayout>.Success(match));
        }

        // Unknown bank, unknown format, or no layout for this (bank, format, type): sniff the content.
        return detector.DetectAsync(fileBytes, fileName, systemLayouts, ct);
    }

    private static string? DetectFormat(byte[] bytes, string fileName)
    {
        if (fileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase))
            return "ofx";
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return "csv";

        var header = ReadHeader(bytes, 2048);
        if (header.Contains("OFXHEADER:", StringComparison.OrdinalIgnoreCase)
            || header.Contains("<OFX>", StringComparison.OrdinalIgnoreCase))
            return "ofx";
        if (header.Contains(',') || header.Contains(';'))
            return "csv";

        return null;
    }

    private static string ReadHeader(byte[] bytes, int maxBytes)
    {
        var slice = bytes[..Math.Min(maxBytes, bytes.Length)];
        try { return Encoding.UTF8.GetString(slice); }
        catch { return Encoding.GetEncoding(1252).GetString(slice); }
    }
}
