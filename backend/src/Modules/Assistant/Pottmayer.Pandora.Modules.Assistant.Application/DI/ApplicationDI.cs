using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Tars.Core.Mediator.DI;

namespace Pottmayer.Pandora.Modules.Assistant.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddAssistantApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        return services;
    }
}
