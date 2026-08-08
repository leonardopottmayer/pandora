using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddNotesPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(PagesController).Assembly);
        return builder;
    }
}
