using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notifications.Infrastructure.Templates;

/// <summary>
/// Renders templates from code, localized to en / pt-BR. A database-backed catalogue
/// (<c>not003_notification_template</c>) is a future step; keeping templates in code (versioned in git)
/// is the minimum.
/// </summary>
public sealed class InMemoryNotificationTemplateRenderer(IOptions<NotificationsOptions> options)
    : INotificationTemplateRenderer
{
    public NotificationContent Render(TemplateKey templateKey, string locale, IReadOnlyDictionary<string, string> payload)
    {
        return templateKey.Value switch
        {
            "account-activation" => RenderAccountActivation(locale, payload),
            _ => throw new InvalidOperationException($"No template registered for key '{templateKey.Value}'.")
        };
    }

    private NotificationContent RenderAccountActivation(string locale, IReadOnlyDictionary<string, string> payload)
    {
        payload.TryGetValue("token", out var token);
        var activationUrl = options.Value.ActivationUrlTemplate
            .Replace("{token}", Uri.EscapeDataString(token ?? string.Empty));

        return locale switch
        {
            "pt-BR" => new NotificationContent(
                Subject: "Ative sua conta no Pandora",
                Body: $"Bem-vindo ao Pandora! Confirme sua conta acessando o link:\n{activationUrl}",
                IsHtml: false),

            _ => new NotificationContent(
                Subject: "Activate your Pandora account",
                Body: $"Welcome to Pandora! Confirm your account using the link:\n{activationUrl}",
                IsHtml: false)
        };
    }
}
