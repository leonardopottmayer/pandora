using Pottmayer.Tars.Communication.Email.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IEmailSender"/>. Succeeds by default; set <see cref="Throw"/> to simulate a
/// provider failure. Captures every message handed to it.
/// </summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public Exception? Throw { get; set; }
    public string Provider { get; set; } = "fake";
    public string? ProviderMessageId { get; set; } = "msg-1";

    public Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        if (Throw is not null)
            throw Throw;
        return Task.FromResult(new EmailDeliveryResult(Provider, ProviderMessageId));
    }
}
