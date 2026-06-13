using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Jobs;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddFinancesInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedService<StatementLifecycleBackgroundService>();
        return builder;
    }
}
