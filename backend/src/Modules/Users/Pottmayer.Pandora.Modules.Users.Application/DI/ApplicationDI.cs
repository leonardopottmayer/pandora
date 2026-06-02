using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Users.Application.Commands.RegisterUser;
using Pottmayer.Pandora.Modules.Users.Application.Services;
using Pottmayer.Pandora.Modules.Users.Contracts.Authentication;
using Pottmayer.Tars.Core.Mediator.DI;

namespace Pottmayer.Pandora.Modules.Users.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddUsersApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserAuthenticator, UserAuthenticatorService>();

        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(RegisterUserCommandHandler).Assembly));

        return services;
    }
}
