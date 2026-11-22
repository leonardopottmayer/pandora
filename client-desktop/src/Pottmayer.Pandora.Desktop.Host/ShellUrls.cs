namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>The two origins the app ever trusts: the user's Pandora server and the shell's own local pages.</summary>
internal static class ShellUrls
{
    /// <summary>Virtual host mapped to the <c>shell/</c> folder (first-run and offline pages).</summary>
    public const string ShellHost = "pandora-desktop.local";

    public static readonly Uri ShellOrigin = new($"https://{ShellHost}/");

    public static string ShellPage(string page, string query = "") => $"https://{ShellHost}/{page}{query}";

    /// <summary>
    /// What the user typed on first run → the server URL. A bare <c>192.168.1.10:8730</c> means http;
    /// anything that is not an absolute http(s) URL is rejected.
    /// </summary>
    public static Uri? TryParseServer(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (!text.Contains("://", StringComparison.Ordinal)) text = "http://" + text;

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Length > 0
            ? uri
            : null;
    }

    /// <summary>Scheme, host and port equal — the browser's notion of origin.</summary>
    public static bool SameOrigin(string? uri, Uri origin) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && Uri.Compare(parsed, origin, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    public static string Origin(Uri uri) => uri.GetLeftPart(UriPartial.Authority);

    public static bool IsWeb(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
}
