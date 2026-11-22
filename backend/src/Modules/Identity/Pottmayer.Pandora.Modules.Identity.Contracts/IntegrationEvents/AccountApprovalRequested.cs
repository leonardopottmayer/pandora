using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by Identity when an account is activated while approval is required, so Channels can
/// tell the approvers. Broker-ready POCO.
/// </summary>
public sealed record AccountApprovalRequested(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Name,
    string Email,
    IReadOnlyList<string> ApproverEmails,
    string Locale) : IIntegrationEvent;
