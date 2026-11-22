using Microsoft.Extensions.DependencyInjection;

namespace Pottmayer.Pandora.Modules.Files.Presentation.DI;

public static class PresentationDI
{
    /// <summary>Registers the Files module's controllers as an MVC application part.</summary>
    public static IMvcBuilder AddFilesPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(PresentationDI).Assembly);
        return builder;
    }
}
