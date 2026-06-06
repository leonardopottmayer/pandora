namespace Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;

/// <summary>
/// Normalizes an incoming locale to one the renderer supports (en, pt-BR), defaulting to en.
/// </summary>
public static class Locale
{
    public const string Default = "en";

    private static readonly string[] Supported = ["en", "pt-BR"];

    public static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return Default;

        return Array.Find(Supported, s => string.Equals(s, locale, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }
}
