using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Identity.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Identity.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddIdentityPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(AuthController).Assembly);
        return builder;
    }
}
