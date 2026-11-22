using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// In-memory <see cref="INotificationTemplateRenderer"/> that echoes the request as content and
/// records every call, so the enqueue flow can be tested without real templates.
/// </summary>
internal sealed class FakeTemplateRenderer : INotificationTemplateRenderer
{
    public List<(TemplateKey TemplateKey, Channel Channel, string Locale, IReadOnlyDictionary<string, string> Payload)> Calls { get; } = [];

    public NotificationContent Content { get; set; } = new("subject", "body", IsHtml: false);

    public NotificationContent Render(
        TemplateKey templateKey, Channel channel, string locale, IReadOnlyDictionary<string, string> payload)
    {
        Calls.Add((templateKey, channel, locale, payload));
        return Content;
    }
}
