namespace Pottmayer.Pandora.Modules.Integrations.Application.Oauth;

/// <summary>Scopes are stored space-separated (the OAuth wire form). This converts to and from a list.</summary>
internal static class ScopeString
{
    public static string Join(IEnumerable<string> scopes) => string.Join(' ', scopes);

    public static IReadOnlyList<string> Split(string scopes) =>
        scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
