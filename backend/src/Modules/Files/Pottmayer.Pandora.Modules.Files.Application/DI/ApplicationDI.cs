using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddFilesApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        // What an administrator can switch off and grant in this module (see the Admin module).
        services.AddTarsFeatureFlagDefinitions(f => f.Add(FilesFeatures.Files));
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(FilesPermissions.Module, feature: FilesFeatures.Files)
                .Resource(FilesPermissions.Roots.Resource).Crud()
                .Resource(FilesPermissions.Scans.Resource).Read().Actions(FilesPermissions.Actions.Run)
                .Resource(FilesPermissions.Entries.Resource).Read()
                .Resource(FilesPermissions.Review.Resource).Read().Update()
                .Resource(FilesPermissions.Filters.Resource).Read().Update().Delete()
                .Resource(FilesPermissions.Preferences.Resource).Read().Update());

        return services;
    }
}
