using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Users.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Users.Infrastructure.Security;

namespace Pottmayer.Pandora.Modules.Users.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IServiceCollection AddUsersInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        return services;
    }
}
