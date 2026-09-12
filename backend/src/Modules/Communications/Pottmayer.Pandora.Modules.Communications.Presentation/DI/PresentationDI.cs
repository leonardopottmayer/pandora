using Microsoft.Extensions.DependencyInjection;

namespace Pottmayer.Pandora.Modules.Communications.Presentation.DI;

public static class PresentationDI
{
    /// <summary>
    /// Registers the Communications module's controllers as an MVC application part. No controllers
    /// exist yet — the assembly is empty of endpoints; the hook exists so the Host registration
    /// mirrors the other modules.
    /// </summary>
    public static IMvcBuilder AddCommunicationsPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(PresentationDI).Assembly);
        return builder;
    }
}
