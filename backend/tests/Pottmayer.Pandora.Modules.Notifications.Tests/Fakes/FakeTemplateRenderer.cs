using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;

/// <summary>
/// In-memory <see cref="INotificationTemplateRenderer"/> that echoes the request as content and
/// records every call, so the enqueue flow can be tested without real templates.
/// </summary>
internal sealed class FakeTemplateRenderer : INotificationTemplateRenderer
{
    public List<(TemplateKey TemplateKey, string Locale, IReadOnlyDictionary<string, string> Payload)> Calls { get; } = [];

    public NotificationContent Content { get; set; } = new("subject", "body", IsHtml: false);

    public NotificationContent Render(TemplateKey templateKey, string locale, IReadOnlyDictionary<string, string> payload)
    {
        Calls.Add((templateKey, locale, payload));
        return Content;
    }
}
