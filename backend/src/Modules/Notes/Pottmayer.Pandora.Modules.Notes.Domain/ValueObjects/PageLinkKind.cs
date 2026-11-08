using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>
/// How a page references another one: named inside the text (a mention, stored as
/// <c>wikilink</c>) or shown as a page-link block (a card, stored as <c>embed</c>). Both are edges in the
/// same graph; the kind only changes how the reference is drawn.
/// </summary>
public sealed class PageLinkKind : IDomainValue<PageLinkKind>
{
    public static readonly PageLinkKind Wikilink = new("wikilink");
    public static readonly PageLinkKind Embed = new("embed");

    private static readonly Dictionary<string, PageLinkKind> All = new()
    {
        [Wikilink.Value] = Wikilink,
        [Embed.Value] = Embed
    };

    public string Value { get; }

    private PageLinkKind(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static PageLinkKind FromValue(string value) =>
        All.TryGetValue(value, out var kind)
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown page link kind.");

    public override string ToString() => Value;
}
