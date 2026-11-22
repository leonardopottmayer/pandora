using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Contracts;

/// <summary>
/// Asks Channels to notify a <em>user</em> about something, naming a category and an intent — not a
/// channel or an address. Channels resolves the user's channels for the category (from their
/// preferences, or the <see cref="Channels"/> override), renders per channel, and fans out into one
/// durable row per channel, all sharing a group id. Broker-ready POCO.
/// </summary>
/// <remarks>
/// This is the per-user path, distinct from the address-explicit <see cref="SendNotificationRequested"/>.
/// Security notifications (identity.*) do not use it: they are mandatory and keep the fact->template
/// subscriber path.
/// </remarks>
public sealed record NotifyUserRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Category,
    string TemplateKey,
    string? Locale,
    IReadOnlyList<string>? Channels,
    IReadOnlyDictionary<string, string> Payload,
    Guid CorrelationId,
    IReadOnlyList<NotificationButton>? Buttons = null) : IIntegrationEvent;

/// <summary>
/// An inline button the caller wants on the message. The label is display text the caller owns
/// (localized on their side); the action and payload are their domain, opaque to Channels and
/// returned intact when the button is tapped. Only channels that support buttons render them.
/// </summary>
public sealed record NotificationButton(string OwnerModule, string Action, string Label, string? Payload = null);
