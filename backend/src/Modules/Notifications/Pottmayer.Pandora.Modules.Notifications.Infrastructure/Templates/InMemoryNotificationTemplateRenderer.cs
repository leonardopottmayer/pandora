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
            "password-reset" => RenderPasswordReset(locale, payload),
            "password-changed" => RenderPasswordChanged(locale),
            "mfa-enabled" => RenderMfaEnabled(locale),
            "mfa-disabled" => RenderMfaDisabled(locale),
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

    private NotificationContent RenderPasswordReset(string locale, IReadOnlyDictionary<string, string> payload)
    {
        payload.TryGetValue("token", out var token);
        var resetUrl = options.Value.PasswordResetUrlTemplate
            .Replace("{token}", Uri.EscapeDataString(token ?? string.Empty));

        return locale switch
        {
            "pt-BR" => new NotificationContent(
                Subject: "Redefina sua senha no Pandora",
                Body: $"Recebemos um pedido para redefinir sua senha. Acesse o link para criar uma nova:\n{resetUrl}\n\nSe não foi você, ignore este e-mail.",
                IsHtml: false),

            _ => new NotificationContent(
                Subject: "Reset your Pandora password",
                Body: $"We received a request to reset your password. Use the link to set a new one:\n{resetUrl}\n\nIf this wasn't you, please ignore this e-mail.",
                IsHtml: false)
        };
    }

    private static NotificationContent RenderPasswordChanged(string locale)
    {
        return locale switch
        {
            "pt-BR" => new NotificationContent(
                Subject: "Sua senha no Pandora foi alterada",
                Body: "Sua senha foi alterada com sucesso. Se não foi você, entre em contato com o suporte imediatamente.",
                IsHtml: false),

            _ => new NotificationContent(
                Subject: "Your Pandora password was changed",
                Body: "Your password was changed successfully. If this wasn't you, contact support immediately.",
                IsHtml: false)
        };
    }

    private static NotificationContent RenderMfaEnabled(string locale)
    {
        return locale switch
        {
            "pt-BR" => new NotificationContent(
                Subject: "Verificação em duas etapas ativada",
                Body: "A verificação em duas etapas (MFA) foi ativada na sua conta Pandora. Se não foi você, redefina sua senha e entre em contato com o suporte imediatamente.",
                IsHtml: false),

            _ => new NotificationContent(
                Subject: "Two-factor authentication enabled",
                Body: "Two-factor authentication (MFA) was enabled on your Pandora account. If this wasn't you, reset your password and contact support immediately.",
                IsHtml: false)
        };
    }

    private static NotificationContent RenderMfaDisabled(string locale)
    {
        return locale switch
        {
            "pt-BR" => new NotificationContent(
                Subject: "Verificação em duas etapas desativada",
                Body: "A verificação em duas etapas (MFA) foi desativada na sua conta Pandora. Se não foi você, redefina sua senha e entre em contato com o suporte imediatamente.",
                IsHtml: false),

            _ => new NotificationContent(
                Subject: "Two-factor authentication disabled",
                Body: "Two-factor authentication (MFA) was disabled on your Pandora account. If this wasn't you, reset your password and contact support immediately.",
                IsHtml: false)
        };
    }
}
