namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports;

/// <summary>
/// What a web page says about itself: its title, description, image and icon, and how the site embeds
/// it (oEmbed) when it does.
/// </summary>
public sealed record LinkPreview(
    string Url,
    string? Title,
    string? Description,
    string? SiteName,
    string? ImageUrl,
    string? IconUrl,
    LinkEmbed? Embed);

/// <summary>
/// An oEmbed answer: <c>video</c> and <c>rich</c> bring the site's own <see cref="Html"/>, <c>photo</c>
/// an image <see cref="Url"/>. The frontend draws the HTML in a sandboxed frame, never in the page.
/// </summary>
public sealed record LinkEmbed(string Type, string? Html, string? Url, int? Width, int? Height);

/// <summary>Reads a public web page for a bookmark or an embed. Never reaches a private network address.</summary>
public interface ILinkPreviewer
{
    /// <summary>The page's preview, or <c>null</c> when it cannot be read (down, not HTML, private address).</summary>
    Task<LinkPreview?> PreviewAsync(Uri url, CancellationToken ct);
}
