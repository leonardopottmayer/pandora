using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Tars.Core.Mediator.DI;

namespace Pottmayer.Pandora.Modules.Identity.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(SignInCommandHandler).Assembly));

        services.AddOptions<AccountActivationOptions>()
                .BindConfiguration(AccountActivationOptions.SectionName);

        return services;
    }
}
