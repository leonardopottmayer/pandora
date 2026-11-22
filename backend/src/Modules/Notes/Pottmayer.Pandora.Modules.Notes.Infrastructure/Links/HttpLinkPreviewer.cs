using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.Links;

/// <summary>
/// Reads a page's head for its preview, and its oEmbed when the site offers one. Every connection goes
/// through <see cref="ConnectPublicAsync"/>, which resolves the host and connects only to a public
/// address, so neither the link nor a redirect can reach the server's own network.
/// </summary>
internal sealed class HttpLinkPreviewer(HttpClient http, ILogger<HttpLinkPreviewer> logger) : ILinkPreviewer
{
    private const int MaxRedirects = 4;
    private const int MaxHtmlBytes = 768 * 1024;
    private const int MaxJsonBytes = 256 * 1024;

    /// <summary>Sites whose pages say little to a robot but answer oEmbed: asked directly.</summary>
    private static readonly Dictionary<string, string> KnownOEmbed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["twitter.com"] = "https://publish.twitter.com/oembed?dnt=true&omit_script=false&url=",
        ["x.com"] = "https://publish.twitter.com/oembed?dnt=true&omit_script=false&url=",
        ["bsky.app"] = "https://embed.bsky.app/oembed?url=",
    };

    public async Task<LinkPreview?> PreviewAsync(Uri url, CancellationToken ct)
    {
        try
        {
            var host = url.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? url.Host[4..] : url.Host;
            if (KnownOEmbed.TryGetValue(host, out var endpoint))
                return await FromOEmbedAsync(url, new Uri(endpoint + Uri.EscapeDataString(url.AbsoluteUri)), null, ct);

            var page = await GetAsync(url, "text/html,application/xhtml+xml", MaxHtmlBytes, ct);
            if (page is null || !page.Value.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase)) return null;
            var meta = LinkMetadata.ParseHtml(page.Value.Body, page.Value.Url);
            var preview = new LinkPreview(page.Value.Url.AbsoluteUri, meta.Title, meta.Description, meta.SiteName, meta.ImageUrl, meta.IconUrl, null);
            return meta.OEmbedUrl is null ? preview : await FromOEmbedAsync(url, meta.OEmbedUrl, preview, ct) ?? preview;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException or UriFormatException or IOException)
        {
            logger.LogInformation(ex, "Link preview of {Host} failed", url.Host);
            return null;
        }
    }

    private async Task<LinkPreview?> FromOEmbedAsync(Uri url, Uri endpoint, LinkPreview? page, CancellationToken ct)
    {
        var answer = await GetAsync(endpoint, "application/json", MaxJsonBytes, ct);
        if (answer is null || LinkMetadata.ParseOEmbed(answer.Value.Body) is not { } oEmbed) return page;
        return new LinkPreview(
            page?.Url ?? url.AbsoluteUri,
            page?.Title ?? oEmbed.Title,
            page?.Description,
            page?.SiteName ?? oEmbed.SiteName,
            page?.ImageUrl ?? oEmbed.ThumbnailUrl,
            page?.IconUrl,
            oEmbed.Embed);
    }

    /// <summary>The body of a GET, following redirects by hand, cut at <paramref name="maxBytes"/>.</summary>
    private async Task<(Uri Url, string MediaType, string Body)?> GetAsync(Uri url, string accept, int maxBytes, CancellationToken ct)
    {
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd(accept);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                var next = location.IsAbsoluteUri ? location : new Uri(url, location);
                if (next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps) return null;
                url = next;
                continue;
            }
            if (!response.IsSuccessStatusCode) return null;

            var body = await ReadCappedAsync(response.Content, maxBytes, ct);
            return (url, response.Content.Headers.ContentType?.MediaType ?? string.Empty, body);
        }
        return null;
    }

    private static async Task<string> ReadCappedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        var buffer = new byte[maxBytes];
        var read = 0;
        while (read < maxBytes)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, maxBytes - read), ct);
            if (count == 0) break;
            read += count;
        }
        return EncodingOf(content.Headers.ContentType).GetString(buffer, 0, read);
    }

    private static Encoding EncodingOf(MediaTypeHeaderValue? type)
    {
        try
        {
            return type?.CharSet is { Length: > 0 } charset ? Encoding.GetEncoding(charset.Trim('"')) : Encoding.UTF8;
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>The handler's connect step: resolve the host, keep only public addresses, connect to one.</summary>
    internal static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
        var allowed = addresses.Where(PublicAddress.Is).ToArray();
        if (allowed.Length == 0) throw new HttpRequestException($"{host} is not a public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>No proxy (it would connect for us, past the check), no automatic redirects, a short leash.</summary>
    internal static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(4),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = ConnectPublicAsync,
    };
}
