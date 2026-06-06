namespace Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// Rendered message content ready to be handed to a provider.
/// </summary>
public sealed record NotificationContent(string Subject, string Body, bool IsHtml);
