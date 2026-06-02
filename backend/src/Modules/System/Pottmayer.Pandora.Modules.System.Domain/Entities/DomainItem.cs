namespace Pottmayer.Pandora.Modules.System.Domain.Entities;

public sealed class DomainItem
{
    public Guid Id { get; private set; }
    public string DomainName { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public string ItemValue { get; private set; } = string.Empty;
    public string? ItemDescription { get; private set; }

    private DomainItem() { }
}
