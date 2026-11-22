using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Templates;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class FileNotificationTemplateRendererTests
{
    private static readonly FileNotificationTemplateRenderer Renderer = new();

    private static IReadOnlyDictionary<string, string> Payload(string activationUrl) =>
        new Dictionary<string, string> { ["activationUrl"] = activationUrl };

    [Fact]
    public void Email_takes_its_subject_from_the_first_line()
    {
        var content = Renderer.Render(
            TemplateKey.Create("account-activation"), Channel.Email, "en", Payload("https://app/activate?token=abc"));

        Assert.Equal("Activate your Pandora account", content.Subject);
        Assert.Contains("https://app/activate?token=abc", content.Body);
        Assert.DoesNotContain("Activate your Pandora account", content.Body);
        Assert.False(content.IsHtml);
    }

    [Fact]
    public void Locale_picks_the_file()
    {
        var content = Renderer.Render(
            TemplateKey.Create("account-activation"), Channel.Email, "pt-BR", Payload("https://app/activate?token=abc"));

        Assert.Equal("Ative sua conta no Pandora", content.Subject);
    }

    [Fact]
    public void Telegram_has_no_subject_and_keeps_the_whole_file_as_body()
    {
        var content = Renderer.Render(
            TemplateKey.Create("channel-test"), Channel.Telegram, "pt-BR", new Dictionary<string, string>());

        Assert.Equal(string.Empty, content.Subject);
        Assert.StartsWith("Esta é uma notificação de teste", content.Body);
    }

    [Fact]
    public void The_same_key_renders_differently_per_channel()
    {
        var email = Renderer.Render(
            TemplateKey.Create("channel-test"), Channel.Email, "en", new Dictionary<string, string>());
        var telegram = Renderer.Render(
            TemplateKey.Create("channel-test"), Channel.Telegram, "en", new Dictionary<string, string>());

        Assert.NotEqual(string.Empty, email.Subject);
        Assert.Equal(string.Empty, telegram.Subject);
    }

    [Fact]
    public void An_unknown_key_throws_instead_of_sending_something_empty()
    {
        Assert.Throws<InvalidOperationException>(() => Renderer.Render(
            TemplateKey.Create("no-such-template"), Channel.Email, "en", new Dictionary<string, string>()));
    }

    [Fact]
    public void Every_declared_variant_has_a_file()
    {
        // The startup check, run as a test: a missing file must never reach a running server.
        Assert.Empty(FileNotificationTemplateRenderer.FindMissingVariants());
    }
}
