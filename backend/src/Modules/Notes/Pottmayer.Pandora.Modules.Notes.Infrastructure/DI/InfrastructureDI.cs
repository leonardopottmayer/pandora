using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using Pottmayer.Pandora.Modules.Notes.Infrastructure.Images;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.DI;

public static class InfrastructureDI
{
    /// <summary>Wires the Notes module's infrastructure: reading and thumbnailing uploaded images.</summary>
    public static IHostApplicationBuilder AddNotesInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        return builder;
    }
}
