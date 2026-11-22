using Microsoft.Extensions.DependencyInjection;

namespace Pottmayer.Pandora.Modules.Communications.Presentation.DI;

public static class PresentationDI
{
    /// <summary>Registers the Communications module's controllers as an MVC application part.</summary>
    public static IMvcBuilder AddCommunicationsPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(PresentationDI).Assembly);
        return builder;
    }
}
