using System.Text.RegularExpressions;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>
/// Finds the library items a page's markdown shows: every <c>/api/v1/notes/media/{id}</c> path, as an
/// image (<c>![…](…)</c>) or a file link (<c>[…](…)</c>). Code, fenced or inline, is skipped the way
/// <see cref="TagParser"/> skips it — a path written as an example is not a use.
/// </summary>
public static partial class MediaReferenceParser
{
    /// <summary>The distinct item ids the text points at, in order of first appearance.</summary>
    public static IReadOnlyList<Guid> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var ids = new List<Guid>();
        foreach (Match match in MediaPathRegex().Matches(TagParser.StripCode(markdown)))
        {
            if (Guid.TryParse(match.Groups["id"].Value, out var id) && !ids.Contains(id))
                ids.Add(id);
        }
        return ids;
    }

    [GeneratedRegex(@"/api/v1/notes/media/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex MediaPathRegex();
}
