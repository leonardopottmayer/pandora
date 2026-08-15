using System.Text.RegularExpressions;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>A tag found in markdown: the name as written plus the key it normalizes to.</summary>
public sealed record TagReference(string Name, string Slug);

/// <summary>
/// Extracts <c>#tag</c> references from a page's markdown — the tags are written in the text, so an
/// exported <c>.md</c> carries them along.
///
/// The <c>#</c> is far more common in prose than <c>[[</c>, so the rules are stricter than
/// <see cref="WikilinkParser"/>'s: it must start a line or follow whitespace (a URL fragment or
/// <c>src/lib#2</c> never fires), a heading is excluded by the space it requires after the
/// <c>#</c>, and code — fenced or inline — is removed before the search, because a <c>#comment</c>
/// in a shell block is not a label.
/// </summary>
public static partial class TagParser
{
    /// <summary>
    /// Every tag in the text, in order of first appearance, one per distinct
    /// <see cref="TagReference.Slug"/>. References that normalize to nothing (<c>#123</c>) are skipped.
    /// </summary>
    public static IReadOnlyList<TagReference> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var text = StripCode(markdown);

        var seen = new HashSet<string>();
        var tags = new List<TagReference>();

        foreach (Match match in TagRegex().Matches(text))
        {
            var raw = match.Groups["tag"].Value;
            var slug = TagName.ToSlug(raw);
            if (slug.Length == 0)
                continue;

            if (seen.Add(slug))
                tags.Add(new TagReference(TagName.ToDisplayName(raw), slug));
        }

        return tags;
    }

    /// <summary>
    /// Blanks out fenced and inline code so what lives in there is never read as a tag. The spans are
    /// replaced by spaces rather than removed, so nothing outside them changes shape.
    /// </summary>
    private static string StripCode(string markdown)
        => InlineCodeRegex().Replace(
            FencedCodeRegex().Replace(markdown, Blank),
            Blank);

    /// <summary>Same length, same line breaks, no content — offsets and line starts stay put.</summary>
    private static string Blank(Match match) =>
        string.Create(match.Length, match.Value, (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = source[i] is '\n' or '\r' ? source[i] : ' ';
        });

    // A tag starts the line or follows whitespace, and runs while the characters can belong to it.
    [GeneratedRegex(@"(?<=^|\s)#(?<tag>[\p{L}\p{N}_/-]+)", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex TagRegex();

    // ``` or ~~~ blocks, closed or running to the end of the document.
    [GeneratedRegex(@"^(?<fence>```|~~~).*?(?:^\k<fence>.*?$|\z)",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex FencedCodeRegex();

    // `inline code`, which never spans a blank line.
    [GeneratedRegex(@"`[^`\r\n]*`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();
}
