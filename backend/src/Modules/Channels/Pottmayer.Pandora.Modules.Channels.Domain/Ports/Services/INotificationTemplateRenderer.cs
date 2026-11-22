using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

/// <summary>
/// Turns a template key plus a flat payload into the text that goes out. It substitutes placeholders
/// and picks a file; it never derives values and never knows where the payload came from.
/// </summary>
public interface INotificationTemplateRenderer
{
    /// <summary>
    /// Renders one variant. An e-mail fills subject, body and the html flag; a channel without a
    /// subject leaves it empty and puts everything in the body.
    /// </summary>
    /// <exception cref="InvalidOperationException">No template exists for this combination.</exception>
    NotificationContent Render(
        TemplateKey templateKey,
        Channel channel,
        string locale,
        IReadOnlyDictionary<string, string> payload);
}
