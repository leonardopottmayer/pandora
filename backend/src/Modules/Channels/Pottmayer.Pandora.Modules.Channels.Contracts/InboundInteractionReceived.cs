using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Contracts;

/// <summary>
/// An inline button was tapped. Channels resolved the callback to the interaction it registered,
/// checked the owner, validity and single use, and hands the tap back to the module that declared the
/// button — routed by <c>inbound.interaction.{ownerModule}.{action}</c>, so only that module wakes.
/// </summary>
public sealed record InboundInteractionReceived(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Channel,
    string OwnerModule,
    string Action,
    string? Payload) : IIntegrationEvent;
