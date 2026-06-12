using Microsoft.Extensions.Hosting;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.DI;

public static class InfrastructureDI
{
    /// <summary>
    /// Wires the Finances infrastructure (background jobs, parsers). Empty for now — the module's
    /// jobs (recurrence generation, statement lifecycle, import parsing) arrive in later phases.
    /// </summary>
    public static IHostApplicationBuilder AddFinancesInfrastructure(this IHostApplicationBuilder builder) =>
        builder;
}
