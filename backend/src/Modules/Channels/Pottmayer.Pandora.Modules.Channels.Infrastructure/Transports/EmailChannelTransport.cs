using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Communication.Email.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;

/// <summary>
/// Delivers over SMTP through the Tars e-mail sender. Every failure is treated as transient: SMTP
/// does not tell us reliably enough that an address is gone for good, and a bounce arrives later and
/// out of band.
/// </summary>
public sealed class EmailChannelTransport(IEmailSender sender) : IChannelTransport
{
    public Channel Channel => Channel.Email;

    public async Task<ChannelDeliveryResult> SendAsync(Notification notification, CancellationToken ct = default)
    {
        var delivery = await sender.SendAsync(
            new EmailMessage(
                To: [notification.Address.Value],
                Subject: notification.Subject,
                Body: notification.Body,
                IsHtml: notification.IsHtml),
            ct);

        return new ChannelDeliveryResult(delivery.Provider, delivery.ProviderMessageId);
    }
}
