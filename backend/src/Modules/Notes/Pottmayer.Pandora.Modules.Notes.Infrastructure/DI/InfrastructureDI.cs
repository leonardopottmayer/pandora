using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using Pottmayer.Pandora.Modules.Notes.Infrastructure.Images;
using Pottmayer.Pandora.Modules.Notes.Infrastructure.Links;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.DI;

public static class InfrastructureDI
{
    /// <summary>
    /// Wires the Notes module's infrastructure: reading and thumbnailing uploaded images, and reading
    /// web pages for link previews.
    /// </summary>
    public static IHostApplicationBuilder AddNotesInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        builder.Services
            .AddHttpClient<ILinkPreviewer, HttpLinkPreviewer>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(8);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PandoraLinkPreview/1.0)");
            })
            .ConfigurePrimaryHttpMessageHandler(HttpLinkPreviewer.CreateHandler);
        return builder;
    }
}
