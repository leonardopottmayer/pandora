using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Files.Infrastructure.Jobs;

namespace Pottmayer.Pandora.Modules.Files.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddFilesInfrastructure(this IHostApplicationBuilder builder)
    {
        // Discards scans whose agent stopped sending batches.
        builder.Services.AddHostedService<ScanExpiryBackgroundService>();

        return builder;
    }
}
