using System.Globalization;
using System.Text;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>
/// Turns a page title into a URL/link-friendly slug: lower-cased, accents stripped, non-alphanumerics
/// collapsed to single hyphens. Per-user uniqueness (a numeric suffix on collision) is the caller's
/// job, since it needs the repository.
/// </summary>
public static class Slugger
{
    private const int MaxLength = 80;

    public static string From(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "untitled";

        var decomposed = title.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        var lastWasHyphen = false;
        foreach (var ch in decomposed)
        {
            // Drop the accent marks that FormD split off from their base letters.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && sb.Length > 0)
            {
                sb.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = sb.ToString().Normalize(NormalizationForm.FormC).Trim('-');

        if (slug.Length > MaxLength)
            slug = slug[..MaxLength].Trim('-');

        return slug.Length == 0 ? "untitled" : slug;
    }
}
