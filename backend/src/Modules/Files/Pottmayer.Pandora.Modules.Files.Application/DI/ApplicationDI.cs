using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Tars.Core.Mediator.DI;

namespace Pottmayer.Pandora.Modules.Files.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddFilesApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        return services;
    }
}
