using Pottmayer.Pandora.Modules.Notifications.Abstractions;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Tars.Communication.Email.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notifications.Application.Commands.DispatchPending;

/// <summary>
/// Drains the durable queue: loads due notifications, hands each to the e-mail sender, and records
/// the outcome (sent / failed-with-backoff / dead). One transaction per batch.
/// </summary>
public sealed class DispatchPendingNotificationsCommandHandler(
    IUnitOfWorkFactory factory,
    IEmailSender emailSender,
    TimeProvider timeProvider)
    : CommandHandlerBase<DispatchPendingNotificationsCommand, DispatchPendingNotificationsResult>
{
    protected override async Task<Result<DispatchPendingNotificationsResult>> HandleAsync(
        DispatchPendingNotificationsCommand request, CancellationToken ct)
    {
        var result = await factory.ExecuteAsync(NotificationsModule.Name, async (context, token) =>
        {
            var notifications = context.AcquireRepository<INotificationRepository>();
            var now = timeProvider.GetUtcNow();
            var due = await notifications.GetDueAsync(now, request.Input.BatchSize, token);

            var sent = 0;
            var failed = 0;
            var dead = 0;

            foreach (var notification in due)
            {
                notification.MarkSending();

                try
                {
                    var delivery = await emailSender.SendAsync(
                        new EmailMessage(
                            To: [notification.Recipient.Value],
                            Subject: notification.Subject,
                            Body: notification.Body,
                            IsHtml: notification.IsHtml),
                        token);

                    notification.MarkSent(delivery.Provider, delivery.ProviderMessageId);
                    sent++;
                }
                catch (Exception ex)
                {
                    notification.MarkFailed(ex.Message, timeProvider);
                    if (notification.Status == NotificationStatus.Dead)
                        dead++;
                    else
                        failed++;
                }

                await notifications.UpdateAsync(notification, token);
            }

            return new DispatchPendingNotificationsResult(sent, failed, dead);
        }, cancellationToken: ct);

        return Ok(result);
    }
}
