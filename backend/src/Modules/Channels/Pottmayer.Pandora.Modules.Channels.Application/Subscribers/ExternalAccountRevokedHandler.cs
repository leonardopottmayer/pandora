using System.Globalization;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Maps Integrations' <see cref="ExternalAccountRevoked"/> to a "reconnect your account" notification.
/// The provider rejected a refresh (<c>invalid_grant</c>): a background job cannot re-consent, so the
/// only fix is the user reconnecting, and the only way they learn of it is a message.
/// </summary>
/// <remarks>
/// It republishes <see cref="NotifyUserRequested"/> rather than enqueuing directly, because the event
/// carries no address: Channels resolves the user's channels (Telegram, e-mail, …) from their
/// preferences the same way an Agenda reminder does. The republish is written to the Channels outbox
/// inside a unit of work so it commits durably, matching every other producer in the monolith.
/// </remarks>
public sealed class ExternalAccountRevokedHandler(IUnitOfWorkFactory factory, IIntegrationEventBus bus)
    : IIntegrationEventHandler<ExternalAccountRevoked>
{
    private const string Category = "integrations.account";
    private const string Template = "integrations.account-revoked";

    public Task HandleAsync(ExternalAccountRevoked @event, CancellationToken cancellationToken = default)
    {
        var notify = new NotifyUserRequested(
            EventId: Guid.CreateVersion7(),
            OccurredAt: @event.OccurredAt,
            UserId: @event.UserId,
            Category: Category,
            TemplateKey: Template,
            Locale: null,   // Channels renders in the user's channel locale.
            Channels: null, // Channels resolves from the user's preference (else every usable channel).
            Payload: new Dictionary<string, string>
            {
                ["provider"] = DisplayName(@event.Provider),
            },
            // The source event id is the correlation id, so the notification de-dups against a re-delivery.
            CorrelationId: @event.EventId);

        return factory.ExecuteAsync(
            ChannelsModule.DatabaseKey,
            (_, token) => bus.PublishAsync(notify, token),
            cancellationToken: cancellationToken);
    }

    /// <summary>A provider id (<c>google</c>) shown to a person reads better title-cased (<c>Google</c>).</summary>
    private static string DisplayName(string provider) =>
        string.IsNullOrEmpty(provider)
            ? provider
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(provider);
}
