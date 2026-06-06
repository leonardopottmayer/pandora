using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Contracts;

/// <summary>
/// Generic escape hatch for ad-hoc / admin sends, when no dedicated producer event exists.
/// The caller chooses channel, template and recipient explicitly. Broker-ready POCO.
/// </summary>
public sealed record SendNotificationRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string Channel,
    string Recipient,
    string TemplateKey,
    string Locale,
    IReadOnlyDictionary<string, string> Payload) : IIntegrationEvent;
