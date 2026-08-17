using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

/// <summary>
/// Renders a template (by key + locale) against a flat payload into ready-to-send content.
/// Templates live in code/files for now; a database-backed catalogue is a future step.
/// </summary>
public interface INotificationTemplateRenderer
{
    NotificationContent Render(TemplateKey templateKey, string locale, IReadOnlyDictionary<string, string> payload);
}
