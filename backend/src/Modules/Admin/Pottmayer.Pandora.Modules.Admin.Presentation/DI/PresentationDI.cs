using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Admin.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Admin.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddAdminPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(AdminController).Assembly);
        return builder;
    }
}
