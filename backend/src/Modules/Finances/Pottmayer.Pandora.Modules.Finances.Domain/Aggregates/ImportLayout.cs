using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A system-defined (or future user-defined) template that tells the parsers how to interpret a
/// specific bank's export format. All mutable behaviour lives in the <c>config</c> JSON; the
/// aggregate itself is effectively read-only from the application's perspective.
/// </summary>
public sealed class ImportLayout : AggregateRoot<Guid>
{
    public Guid? UserId { get; private set; }
    public string LayoutCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? BankName { get; private set; }
    public LayoutFileFormat FileFormat { get; private set; } = null!;
    public ImportLayoutAccountType AccountType { get; private set; } = null!;
    public string Config { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsSystemLayout => UserId is null;
    public bool IsOfx => FileFormat == LayoutFileFormat.Ofx;
    public bool IsCsv => FileFormat == LayoutFileFormat.Csv;
    public bool IsCardLayout => AccountType == ImportLayoutAccountType.Card;

    private ImportLayout() { }
}
