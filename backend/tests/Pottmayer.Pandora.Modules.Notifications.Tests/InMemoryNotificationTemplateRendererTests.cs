using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Notifications.Infrastructure;
using Pottmayer.Pandora.Modules.Notifications.Infrastructure.Templates;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notifications.Tests;

public sealed class InMemoryNotificationTemplateRendererTests
{
    private static InMemoryNotificationTemplateRenderer Build(string urlTemplate = "https://app/activate?token={token}")
        => new(Options.Create(new NotificationsOptions { ActivationUrlTemplate = urlTemplate }));

    private static IReadOnlyDictionary<string, string> Payload(string token) => new Dictionary<string, string> { ["token"] = token };

    [Fact]
    public void Renders_english_activation_by_default()
    {
        var content = Build().Render(TemplateKey.Create("account-activation"), "en", Payload("abc"));

        Assert.Equal("Activate your Pandora account", content.Subject);
        Assert.Contains("https://app/activate?token=abc", content.Body);
        Assert.False(content.IsHtml);
    }

    [Fact]
    public void Renders_portuguese_activation()
    {
        var content = Build().Render(TemplateKey.Create("account-activation"), "pt-BR", Payload("abc"));

        Assert.Equal("Ative sua conta no Pandora", content.Subject);
        Assert.Contains("https://app/activate?token=abc", content.Body);
    }

    [Fact]
    public void Falls_back_to_english_for_unknown_locale()
    {
        var content = Build().Render(TemplateKey.Create("account-activation"), "fr", Payload("abc"));

        Assert.Equal("Activate your Pandora account", content.Subject);
    }

    [Fact]
    public void Escapes_the_token_in_the_activation_url()
    {
        var content = Build().Render(TemplateKey.Create("account-activation"), "en", Payload("a b&c"));

        Assert.Contains("token=a%20b%26c", content.Body);
    }

    [Fact]
    public void Handles_a_missing_token_gracefully()
    {
        var content = Build().Render(TemplateKey.Create("account-activation"), "en", new Dictionary<string, string>());

        Assert.Contains("https://app/activate?token=", content.Body);
    }

    [Fact]
    public void Throws_for_an_unregistered_template()
    {
        var renderer = Build();

        Assert.Throws<InvalidOperationException>(
            () => renderer.Render(TemplateKey.Create("unknown-template"), "en", Payload("abc")));
    }
}
