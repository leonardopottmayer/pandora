using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Finances.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Finances.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddFinancesPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(AuditController).Assembly);
        return builder;
    }
}
