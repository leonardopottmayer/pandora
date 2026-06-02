using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Users.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Users.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddUsersPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(UsersController).Assembly);
        return builder;
    }
}
