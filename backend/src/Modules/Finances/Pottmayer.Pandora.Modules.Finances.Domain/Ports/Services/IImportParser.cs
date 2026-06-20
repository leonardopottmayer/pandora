using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

/// <summary>
/// Converts raw file bytes into a list of parsed rows. Each implementation handles one file
/// format (OFX or CSV); the layout config steers bank-specific quirks.
/// </summary>
public interface IImportParser
{
    bool CanParse(ImportLayout layout);

    Task<IReadOnlyList<ParsedImportRow>> ParseAsync(
        byte[] fileBytes, ImportLayout layout, CancellationToken ct = default);
}

/// <summary>
/// Structured representation of a single row after parsing. Amount is always positive; the
/// <see cref="IsCredit"/> flag indicates the direction relative to the user's balance.
/// </summary>
public sealed record ParsedImportRow(
    int RowIndex,
    string RawData,
    string Description,
    string? Payee,
    DateOnly OccurredOn,
    decimal Amount,
    bool IsCredit,
    string Currency,
    string? ExternalId,
    short? InstallmentNumber,
    short? InstallmentCount,
    bool ShouldSkip = false);
