using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Integrations.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Integrations.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddIntegrationsPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(IntegrationsController).Assembly);
        return builder;
    }
}
