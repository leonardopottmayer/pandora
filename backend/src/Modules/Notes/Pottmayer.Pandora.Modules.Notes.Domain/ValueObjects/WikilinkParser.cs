using System.Text.RegularExpressions;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>A reference found in markdown: the raw target text plus how it was written.</summary>
public sealed record WikilinkReference(string Target, PageLinkKind Kind);

/// <summary>
/// Extracts <c>[[Target]]</c> / <c>[[Target|alias]]</c> / <c>![[Target]]</c> references from a page's
/// markdown. Only the target text is returned — matching it against an actual page needs the
/// repository and happens in the application layer.
/// </summary>
public static partial class WikilinkParser
{
    /// <summary>
    /// Every reference in the text, in order, without repeats. Empty targets are skipped, and a target
    /// written twice yields one reference per kind (an edge is a fact, not a count).
    /// </summary>
    public static IReadOnlyList<WikilinkReference> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var seen = new HashSet<(string, string)>();
        var references = new List<WikilinkReference>();

        foreach (Match match in WikilinkRegex().Matches(markdown))
        {
            var target = match.Groups["target"].Value.Trim();
            if (target.Length == 0)
                continue;

            var kind = match.Groups["embed"].Success ? PageLinkKind.Embed : PageLinkKind.Wikilink;

            if (seen.Add((target.ToLowerInvariant(), kind.Value)))
                references.Add(new WikilinkReference(target, kind));
        }

        return references;
    }

    // The target stops at the alias pipe; brackets inside are what tell a wikilink apart from a
    // markdown link followed by an array-ish "[[...]]" literal.
    [GeneratedRegex(@"(?<embed>!)?\[\[(?<target>[^\[\]|]*)(?:\|[^\[\]]*)?\]\]", RegexOptions.Compiled)]
    private static partial Regex WikilinkRegex();
}
