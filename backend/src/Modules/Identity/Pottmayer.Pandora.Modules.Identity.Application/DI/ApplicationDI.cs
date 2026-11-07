using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;
using Pottmayer.Pandora.Modules.Identity.Application.Devices;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Preferences;
using Pottmayer.Pandora.Modules.Identity.Application.Services;
using Pottmayer.Pandora.Modules.Identity.Application.Users;
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

        // The port other modules consume to check a device is the user's (e.g. Files roots).
        services.AddScoped<IDeviceReader, DeviceReader>();

        // The port Admin consumes to find the accounts its bootstrap must promote.
        services.AddScoped<IUserDirectory, UserDirectory>();

        // The effective-zone resolver other modules consume so a missing preference resolves to the
        // configured default instead of UTC.
        services.AddScoped<IEffectiveTimeZoneResolver, EffectiveTimeZoneResolver>();

        services.AddOptions<TimeZoneOptions>()
                .BindConfiguration(TimeZoneOptions.SectionName);

        services.AddOptions<AccountActivationOptions>()
                .BindConfiguration(AccountActivationOptions.SectionName);

        services.AddOptions<AccountApprovalOptions>()
                .BindConfiguration(AccountApprovalOptions.SectionName);
        services.AddScoped<AccountApprovalRule>();

        services.AddOptions<PasswordResetOptions>()
                .BindConfiguration(PasswordResetOptions.SectionName);

        services.AddOptions<MfaOptions>()
                .BindConfiguration(MfaOptions.SectionName);

        return services;
    }
}
