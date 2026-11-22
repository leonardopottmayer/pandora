using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Assistant.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Assistant.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddAssistantPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(AssistantController).Assembly);
        return builder;
    }
}
