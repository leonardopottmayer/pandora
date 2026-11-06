using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;

namespace Pottmayer.Pandora.Modules.Admin.Infrastructure.Jobs;

/// <summary>
/// Promotes the bootstrap e-mails to <c>admin</c> once, at startup. A failure is logged, not fatal: the
/// rest of Pandora keeps working, and the next activation or restart tries again.
/// </summary>
internal sealed class AdminBootstrapService(IServiceScopeFactory scopeFactory, ILogger<AdminBootstrapService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().RunAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The admin bootstrap failed.");
        }
    }
}
