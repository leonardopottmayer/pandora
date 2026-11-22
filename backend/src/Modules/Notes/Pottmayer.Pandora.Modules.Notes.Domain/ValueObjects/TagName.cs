using System.Globalization;
using System.Text;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>
/// Normalizes a tag as written in markdown into the key that identifies it. Two spellings of the
/// same idea collapse into one tag: <c>#Café</c> and <c>#cafe</c> share the slug <c>cafe</c>, while
/// the name the author first typed is what gets displayed.
///
/// Unlike <see cref="Slugger"/> (which flattens everything to hyphens), this keeps <c>/</c> and
/// <c>_</c>: they are part of how a tag is written — <c>#projeto/pandora</c> is one tag, not two.
/// </summary>
public static class TagName
{
    /// <summary>Longer than this is prose, not a label.</summary>
    public const int MaxLength = 50;

    /// <summary>
    /// The lookup key for a tag, or an empty string when nothing usable is left. Lower-cased, accents
    /// stripped, and trimmed of the separators that cannot start or end a tag.
    /// </summary>
    public static string ToSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var decomposed = raw.Trim().TrimStart('#').ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            // Drop the accent marks that FormD split off from their base letters.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (IsTagChar(ch))
                sb.Append(ch);
        }

        var slug = sb.ToString().Normalize(NormalizationForm.FormC).Trim('-', '_', '/');

        if (slug.Length > MaxLength)
            slug = slug[..MaxLength].Trim('-', '_', '/');

        // A tag made only of digits and separators is a number in the text ("#123"), not a label.
        return slug.Any(char.IsLetter) ? slug : string.Empty;
    }

    /// <summary>Whether the character may appear inside a tag.</summary>
    public static bool IsTagChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '-' or '_' or '/';

    /// <summary>The display form: what the author typed, without the <c>#</c> and capped in length.</summary>
    public static string ToDisplayName(string raw)
    {
        var name = raw.Trim().TrimStart('#').Trim('-', '_', '/');
        return name.Length > MaxLength ? name[..MaxLength] : name;
    }
}
