using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddAgendaPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(RemindersController).Assembly);
        return builder;
    }
}
