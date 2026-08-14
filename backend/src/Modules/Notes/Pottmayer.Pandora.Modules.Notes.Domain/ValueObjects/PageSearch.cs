using System.Text.RegularExpressions;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>
/// Text handling for the full-text search of pages: turning what the user typed into a Postgres
/// <c>tsquery</c>, and cutting the excerpt shown next to a hit.
/// </summary>
public static partial class PageSearch
{
    /// <summary>How much of the body the excerpt keeps around the first match.</summary>
    public const int ExcerptLength = 160;

    /// <summary>
    /// The <c>to_tsquery</c> expression for what the user typed: every word must be present, and the
    /// last character of each is a prefix (<c>:*</c>) so a palette matches while it is still being
    /// typed. Returns an empty string when nothing searchable was typed — the caller answers with no
    /// results rather than querying.
    /// </summary>
    public static string ToTsQuery(string? term)
    {
        var words = Words(term);
        return words.Count == 0 ? string.Empty : string.Join(" & ", words.Select(w => $"{w}:*"));
    }

    /// <summary>
    /// A one-line excerpt of <paramref name="content"/> around the first word of the term, or the head
    /// of the content when no word occurs in the body (a title-only hit). Ellipses mark a cut.
    /// </summary>
    public static string Excerpt(string? content, string? term)
    {
        var text = Whitespace().Replace(content ?? string.Empty, " ").Trim();
        if (text.Length == 0)
            return string.Empty;

        var start = 0;
        foreach (var word in Words(term))
        {
            var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
                continue;

            // Keep a little context before the match instead of starting right on it.
            start = Math.Max(0, at - 30);
            break;
        }

        if (start + ExcerptLength >= text.Length)
            start = Math.Max(0, text.Length - ExcerptLength);

        var excerpt = text.Substring(start, Math.Min(ExcerptLength, text.Length - start));

        return (start > 0 ? "..." : string.Empty)
             + excerpt
             + (start + excerpt.Length < text.Length ? "..." : string.Empty);
    }

    /// <summary>
    /// The searchable words of the term, lower-cased. Punctuation is dropped rather than escaped:
    /// the term reaches Postgres as a <c>tsquery</c> expression, so nothing the user types may be
    /// operator syntax.
    /// </summary>
    private static IReadOnlyList<string> Words(string? term) =>
        string.IsNullOrWhiteSpace(term)
            ? []
            : [.. WordRegex().Matches(term).Select(m => m.Value.ToLowerInvariant())];

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex Whitespace();
}
