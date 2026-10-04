using System.Collections.Concurrent;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Ingress;

/// <summary>
/// Carries an album's caption to its other files. Telegram sends an album as one message per file, all
/// sharing a media group id, and puts the caption on only one of them (the first, from the official apps).
/// </summary>
/// <remarks>
/// A singleton because an album can straddle two polls, each with its own scope. It keeps one entry per
/// chat — the latest captioned album — so it stays bounded without eviction; group ids are unique, so a
/// stale entry never matches a new album.
/// ponytail: in memory and forward only — a restart mid-album, or a caption arriving after bare files of
/// its album, leaves those files uncaptioned (the bot then asks where they go). Persist per group if that bites.
/// </remarks>
public sealed class TelegramAlbumCaptions
{
    private readonly ConcurrentDictionary<(string Bot, string ChatId), (string GroupId, string Caption)> _latest = new();

    /// <summary>The message's own text, or — for a bare file in a known album — the album's caption.</summary>
    public string? Apply(string bot, string chatId, string? mediaGroupId, string? text)
    {
        if (mediaGroupId is null)
            return text;

        if (!string.IsNullOrWhiteSpace(text))
        {
            _latest[(bot, chatId)] = (mediaGroupId, text);
            return text;
        }

        return _latest.TryGetValue((bot, chatId), out var album) && album.GroupId == mediaGroupId
            ? album.Caption
            : text;
    }
}
