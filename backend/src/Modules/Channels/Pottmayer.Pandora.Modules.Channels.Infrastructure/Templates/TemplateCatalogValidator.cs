using Microsoft.Extensions.Hosting;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Templates;

/// <summary>
/// Fails the boot when a declared template variant has no file. A missing template is otherwise
/// invisible until the notification that needed it is already due, at which point it fails inside a
/// background worker where nobody is looking.
/// </summary>
internal sealed class TemplateCatalogValidator : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missing = FileNotificationTemplateRenderer.FindMissingVariants();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Missing notification template files: " + string.Join(", ", missing) +
                ". Every key in the catalogue needs one file per allowed channel and locale.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
