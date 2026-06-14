using System.Resources;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Tars.Core.Localization;
using Pottmayer.Tars.Core.Localization.DI;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.DI;

public static class LocalizationDI
{
    /// <summary>
    /// Registers the Finances module's display resources (category names, system transaction
    /// descriptions) as a message source. The composite provider merges it with the host's, so
    /// <see cref="Pottmayer.Tars.Core.Localization.Abstractions.IMessageProvider"/> resolves these
    /// keys by the request culture. Call after <c>AddTarsLocalization()</c>.
    /// </summary>
    public static IServiceCollection AddFinancesLocalization(this IServiceCollection services)
    {
        var resourceManager = new ResourceManager(
            "Pottmayer.Pandora.Modules.Finances.Infrastructure.Localization.FinancesMessages",
            typeof(LocalizationDI).Assembly);

        services.AddTarsMessageSource(new ResourceManagerMessageSource(resourceManager));

        return services;
    }
}
