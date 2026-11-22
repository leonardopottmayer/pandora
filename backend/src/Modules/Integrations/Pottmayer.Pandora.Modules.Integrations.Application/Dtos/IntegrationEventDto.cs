namespace Pottmayer.Pandora.Modules.Integrations.Application.Dtos;

/// <summary>One entry of the connection event log, as shown in the connection-health view.</summary>
public sealed record IntegrationEventDto(
    Guid Id,
    Guid? ExternalAccountId,
    string Provider,
    string EventType,
    string? Detail,
    DateTimeOffset OccurredAt);
