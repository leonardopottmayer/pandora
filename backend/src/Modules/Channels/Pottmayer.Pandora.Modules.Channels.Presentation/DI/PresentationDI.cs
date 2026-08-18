using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Channels.Presentation.Controllers;

namespace Pottmayer.Pandora.Modules.Channels.Presentation.DI;

public static class PresentationDI
{
    public static IMvcBuilder AddChannelsPresentationPart(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(ChannelsController).Assembly);
        return builder;
    }
}
