using System.Globalization;
using System.Text.Json;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.Imap;

/// <summary>
/// What the IMAP adapter keeps in the mailbox's opaque cursors, per folder (by full path). UIDs are only
/// meaningful under one UIDVALIDITY, so every entry carries it.
/// </summary>
internal static class ImapCursors
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <param name="UidValidity">A change means the server renumbered the folder.</param>
    /// <param name="UidNext">The first UID not seen yet: everything at or above it is new.</param>
    /// <param name="HighestModSeq">Only when the server has CONDSTORE: flag changes since it.</param>
    internal sealed record FolderState(uint UidValidity, uint UidNext, ulong? HighestModSeq);

    /// <param name="Below">The backfill still has to read the UIDs below this one.</param>
    internal sealed record BackfillState(uint UidValidity, uint Below);

    public static string Write<T>(Dictionary<string, T> folders) => JsonSerializer.Serialize(folders, Json);

    public static Dictionary<string, T> Read<T>(string cursor) =>
        JsonSerializer.Deserialize<Dictionary<string, T>>(cursor, Json) ?? [];

    /// <summary>
    /// <c>"{uidvalidity}:{uid}:{folder path}"</c> — the path last, since it may contain <c>:</c>.
    /// The UIDVALIDITY keeps a renumbered folder's new UIDs from colliding with the old rows.
    /// </summary>
    public static string MessageId(uint uidValidity, uint uid, string folder) =>
        string.Create(CultureInfo.InvariantCulture, $"{uidValidity}:{uid}:{folder}");

    public static bool TryParseMessageId(string id, out uint uidValidity, out uint uid, out string folder)
    {
        uidValidity = 0;
        uid = 0;
        folder = string.Empty;

        var parts = id.Split(':', 3);
        if (parts.Length != 3 || parts[2].Length == 0)
            return false;

        folder = parts[2];
        return uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out uidValidity)
               && uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uid);
    }
}
