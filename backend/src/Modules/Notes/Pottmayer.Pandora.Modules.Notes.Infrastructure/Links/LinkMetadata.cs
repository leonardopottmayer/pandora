using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.Links;

/// <summary>What the head of an HTML page says: Open Graph and Twitter tags, the title, the icon, an oEmbed link.</summary>
internal sealed record PageMetadata(string? Title, string? Description, string? SiteName, string? ImageUrl, string? IconUrl, Uri? OEmbedUrl);

/// <summary>
/// Reads link metadata out of HTML and oEmbed JSON. Regular expressions over the tags, not a parser:
/// only the head's <c>meta</c>, <c>link</c> and <c>title</c> matter, and a page that hides them just
/// gets a plainer card.
/// </summary>
internal static partial class LinkMetadata
{
    private const int MaxTitle = 300;
    private const int MaxDescription = 600;

    [GeneratedRegex(@"<(meta|link)\b([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"([\w:-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))")]
    private static partial Regex AttributePattern();

    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitlePattern();

    public static PageMetadata ParseHtml(string html, Uri pageUrl)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? icon = null;
        Uri? oEmbed = null;

        foreach (Match tag in TagPattern().Matches(html))
        {
            var attributes = Attributes(tag.Groups[2].Value);
            if (tag.Groups[1].Value.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                var key = attributes.GetValueOrDefault("property") ?? attributes.GetValueOrDefault("name");
                if (key is not null && attributes.TryGetValue("content", out var content)) meta.TryAdd(key, content);
                continue;
            }

            var rel = attributes.GetValueOrDefault("rel")?.ToLowerInvariant() ?? string.Empty;
            var href = attributes.GetValueOrDefault("href");
            if (href is null) continue;
            if (icon is null && rel.Split(' ').Contains("icon")) icon = Absolute(href, pageUrl);
            if (oEmbed is null && rel == "alternate" && attributes.GetValueOrDefault("type")?.ToLowerInvariant() == "application/json+oembed"
                && Absolute(href, pageUrl) is { } found)
                oEmbed = new Uri(found);
        }

        var title = meta.GetValueOrDefault("og:title") ?? meta.GetValueOrDefault("twitter:title") ?? TitlePattern().Match(html) switch
        {
            { Success: true } match => match.Groups[1].Value,
            _ => null,
        };
        var description = meta.GetValueOrDefault("og:description") ?? meta.GetValueOrDefault("twitter:description") ?? meta.GetValueOrDefault("description");
        var image = meta.GetValueOrDefault("og:image") ?? meta.GetValueOrDefault("og:image:url") ?? meta.GetValueOrDefault("twitter:image");

        return new PageMetadata(
            Clean(title, MaxTitle),
            Clean(description, MaxDescription),
            Clean(meta.GetValueOrDefault("og:site_name"), MaxTitle),
            image is null ? null : Absolute(WebUtility.HtmlDecode(image), pageUrl),
            icon ?? Absolute("/favicon.ico", pageUrl),
            oEmbed);
    }

    /// <summary>An oEmbed answer; <c>null</c> when it is not one.</summary>
    public static (LinkEmbed Embed, string? Title, string? SiteName, string? ThumbnailUrl)? ParseOEmbed(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var type = Text(root, "type")?.ToLowerInvariant();
            if (type is not ("video" or "rich" or "photo" or "link")) return null;
            var embed = new LinkEmbed(type, Text(root, "html"), Http(Text(root, "url")), Number(root, "width"), Number(root, "height"));
            return (embed, Clean(Text(root, "title") ?? Text(root, "author_name"), MaxTitle), Clean(Text(root, "provider_name"), MaxTitle), Http(Text(root, "thumbnail_url")));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> Attributes(string text)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributePattern().Matches(text))
        {
            var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value;
            attributes.TryAdd(match.Groups[1].Value, value);
        }
        return attributes;
    }

    private static string? Clean(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var decoded = Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
        if (decoded.Length == 0) return null;
        return decoded.Length > max ? decoded[..max].TrimEnd() + "…" : decoded;
    }

    /// <summary>An address on the page made absolute; only http and https are kept.</summary>
    private static string? Absolute(string href, Uri pageUrl) =>
        Uri.TryCreate(pageUrl, href.Trim(), out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url.AbsoluteUri
            : null;

    private static string? Http(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            ? parsed.AbsoluteUri
            : null;

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
}
