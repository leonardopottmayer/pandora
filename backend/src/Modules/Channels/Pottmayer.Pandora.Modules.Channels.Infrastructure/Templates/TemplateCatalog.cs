using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Templates;

/// <summary>
/// Declares which template variants must exist. Every key names the channels it is allowed to go out
/// on, and every combination of key, channel and locale must resolve to a file.
/// </summary>
/// <remarks>
/// Security mail (<c>identity.*</c>) is e-mail only: it is not a preference, and a password reset
/// that quietly went to a chat instead of an inbox would be a worse outcome, not a nicer one.
/// </remarks>
internal static class TemplateCatalog
{
    public static readonly IReadOnlyList<string> Locales = ["en", "pt-BR"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<Channel>> Keys =
        new Dictionary<string, IReadOnlyList<Channel>>
        {
            ["account-activation"] = [Channel.Email],
            ["password-reset"] = [Channel.Email],
            ["password-changed"] = [Channel.Email],
            ["mfa-enabled"] = [Channel.Email],
            ["mfa-disabled"] = [Channel.Email],
            ["channel-test"] = [Channel.Email, Channel.Telegram],
            ["agenda.reminder.due"] = [Channel.Email, Channel.Telegram],
        };

    /// <summary>Every variant that has to be present, for the startup check to walk.</summary>
    public static IEnumerable<(string Key, Channel Channel, string Locale)> AllVariants()
    {
        foreach (var (key, channels) in Keys)
        {
            foreach (var channel in channels)
            {
                foreach (var locale in Locales)
                    yield return (key, channel, locale);
            }
        }
    }

    /// <summary>Whether a key is allowed to be delivered on a channel at all.</summary>
    public static bool Allows(string key, Channel channel) =>
        Keys.TryGetValue(key, out var channels) && channels.Contains(channel);
}
