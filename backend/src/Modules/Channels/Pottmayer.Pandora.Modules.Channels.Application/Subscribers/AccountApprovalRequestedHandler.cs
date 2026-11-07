using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="AccountApprovalRequested"/> to the <c>account-approval-requested</c>
/// template and e-mails each approver. One delivery per approver, each with its own idempotency key.
/// </summary>
public sealed class AccountApprovalRequestedHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<AccountApprovalRequested>
{
    private static readonly TemplateKey Template = TemplateKey.Create("account-approval-requested");

    public async Task HandleAsync(AccountApprovalRequested @event, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["userId"] = @event.UserId.ToString(),
            ["name"] = @event.Name,
            ["email"] = @event.Email,
        };

        foreach (var approver in @event.ApproverEmails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await enqueuer.EnqueueAsync(
                Channel.Email,
                approver,
                Template,
                Locale.Normalize(@event.Locale),
                payload,
                ApproverEventId(@event.EventId, approver),
                category: "identity.account",
                ct: cancellationToken);
        }
    }

    // A stable id per (event, approver): a redelivered event enqueues nothing twice.
    private static Guid ApproverEventId(Guid eventId, string approver)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{eventId:N}:{approver.ToLowerInvariant()}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
