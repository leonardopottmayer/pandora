using System.Text;
using MimeKit.Text;

namespace Pottmayer.Pandora.Modules.Communications.Application.Mime;

/// <summary>Extracts literal mail text without executing HTML or loading external resources.</summary>
internal static class MailNoteText
{
    public static string Read(string? textBody, string? htmlBody)
    {
        if (!string.IsNullOrWhiteSpace(textBody)) return textBody;
        if (string.IsNullOrWhiteSpace(htmlBody)) return string.Empty;
        using var reader = new StringReader(htmlBody);
        var tokenizer = new HtmlTokenizer(reader) { DecodeCharacterReferences = true };
        var text = new StringBuilder();
        string? hidden = null;
        while (tokenizer.ReadNextToken(out var token))
        {
            if (token is HtmlTagToken tag)
            {
                var name = tag.Name.ToLowerInvariant();
                if (hidden is not null)
                {
                    if ((tag.IsEndTag && name == hidden) || (hidden == "head" && name == "body")) hidden = null;
                    continue;
                }
                if (!tag.IsEndTag && name is "head" or "script" or "style" or "template" or "noscript")
                {
                    if (!tag.IsEmptyElement) hidden = name;
                }
                else if (name is "p" or "div" or "br" or "li" or "tr" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "blockquote" or "table")
                    text.Append('\n');
                else if (name is "td" or "th") text.Append(' ');
            }
            else if (token is HtmlDataToken data && hidden is null)
                text.Append(data.Data);
        }
        return text.ToString().Trim();
    }
}
