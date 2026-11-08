using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Xunit;

namespace Pottmayer.Pandora.Modules.Communications.Tests;

public sealed class MailNoteTextTests
{
    [Fact]
    public void Prefers_the_literal_plain_text_part()
    {
        Assert.Equal("# literal\r\n<not markup>", MailNoteText.Read("# literal\r\n<not markup>", "<p>Other</p>"));
    }

    [Fact]
    public void Html_text_decodes_entities_preserves_separation_and_ignores_active_content_and_tracking_urls()
    {
        var text = MailNoteText.Read(null, """
            <html><head><title>Metadata</title><style>.hidden{display:none}</style></head><body>
            <p>Hello &amp; world</p><div>Second<br>line</div><ul><li>One</li><li>Two</li></ul>
            <script>alert('secret')</script><!-- ignored comment --><img src="https://tracker.test/pixel">
            <p>3 &lt; 5</p></body></html>
            """);
        Assert.Contains("Hello & world", text);
        Assert.Contains("Second\nline", text);
        Assert.Contains("One", text);
        Assert.Contains("Two", text);
        Assert.Contains("3 < 5", text);
        Assert.DoesNotContain("Metadata", text);
        Assert.DoesNotContain("display:none", text);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("ignored comment", text);
        Assert.DoesNotContain("tracker.test", text);
    }

    [Fact]
    public void Incomplete_html_and_empty_bodies_remain_readable_without_markup()
    {
        Assert.Equal("Hello", MailNoteText.Read(" ", "<div><p>Hello"));
        Assert.Equal("", MailNoteText.Read(null, null));
    }
}
