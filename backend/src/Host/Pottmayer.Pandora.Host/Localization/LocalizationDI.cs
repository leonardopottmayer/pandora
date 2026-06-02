using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Tars.Core.Localization;
using Pottmayer.Tars.Core.Localization.DI;
using Pottmayer.Tars.Web.Http.DI;
using System.Resources;

namespace Pottmayer.Pandora.Host.Localization;

public static class LocalizationDI
{
    public static IServiceCollection AddPandoraLocalization(this IServiceCollection services)
    {
        services.AddTarsHttpErrorMapper<LocalizedHttpErrorMapper>();

        var resourceManager = new ResourceManager(
            "Pottmayer.Pandora.Host.Localization.PandoraMessages",
            typeof(LocalizationDI).Assembly);

        services.AddTarsMessageSource(new ResourceManagerMessageSource(resourceManager));

        return services;
    }
}
