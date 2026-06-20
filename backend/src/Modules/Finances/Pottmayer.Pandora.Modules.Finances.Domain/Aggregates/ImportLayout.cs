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
    public string FileFormat { get; private set; } = string.Empty;
    public string AccountType { get; private set; } = string.Empty;
    public string Config { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsSystemLayout => UserId is null;
    public bool IsOfx => FileFormat == "ofx";
    public bool IsCsv => FileFormat == "csv";
    public bool IsCardLayout => AccountType == "card";

    private ImportLayout() { }
}
