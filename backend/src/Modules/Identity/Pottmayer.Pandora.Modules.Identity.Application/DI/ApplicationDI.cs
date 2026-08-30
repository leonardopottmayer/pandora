using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Preferences;
using Pottmayer.Tars.Core.Mediator.DI;

namespace Pottmayer.Pandora.Modules.Identity.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(SignInCommandHandler).Assembly));

        // The port other modules consume to read a user's scheduling defaults.
        services.AddScoped<IUserPreferencesReader, UserPreferencesReader>();

        services.AddOptions<AccountActivationOptions>()
                .BindConfiguration(AccountActivationOptions.SectionName);

        services.AddOptions<PasswordResetOptions>()
                .BindConfiguration(PasswordResetOptions.SectionName);

        services.AddOptions<MfaOptions>()
                .BindConfiguration(MfaOptions.SectionName);

        return services;
    }
}
