using Microsoft.Extensions.Hosting;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.DI;

public static class InfrastructureDI
{
    /// <summary>
    /// Wires the Notes module's infrastructure. Nothing here yet — attachment storage and background
    /// work arrive in later phases; the hook exists so the Host registration mirrors the other modules.
    /// </summary>
    public static IHostApplicationBuilder AddNotesInfrastructure(this IHostApplicationBuilder builder)
        => builder;
}
