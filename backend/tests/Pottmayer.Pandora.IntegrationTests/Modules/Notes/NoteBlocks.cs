using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Page documents for the Notes tests, built the way the editor sends them: blocks with ids, inline
/// text and mentions. Serialized as the request's <c>content</c>.
/// </summary>
internal static class NoteBlocks
{
    public static object Doc(params object[] blocks) => new { version = 1, blocks };

    /// <summary>A document of one paragraph.</summary>
    public static object Text(params object[] parts) => Doc(Paragraph(parts));

    /// <summary>Strings become text, <see cref="Mention"/>s page mentions.</summary>
    public static object Paragraph(params object[] parts) =>
        new { id = Id(), type = "paragraph", content = parts.Select(Inline).ToArray() };

    public static object Heading(int level, string text) =>
        new { id = Id(), type = "heading", props = new { level }, content = new[] { new { text } } };

    public static MentionPart Mention(Guid page) => new(page);

    public static object PageLink(Guid page) => new { id = Id(), type = "page-link", props = new { pageId = page } };

    public static object Tags(params string[] tags) => new { id = Id(), type = "tags", props = new { tags } };

    public static object Image(Guid mediaId) => new { id = Id(), type = "image", props = new { mediaId } };

    /// <summary>The readable text of a stored document, to compare versions and pages by.</summary>
    public static string TextOf(string json) => PageContent.Parse(json).Text;

    public sealed record MentionPart(Guid Page);

    private static object Inline(object part) => part switch
    {
        string s => new { text = s },
        MentionPart m => new { mention = new { page = m.Page } },
        _ => part
    };

    private static string Id() => Guid.NewGuid().ToString("N")[..12];
}
