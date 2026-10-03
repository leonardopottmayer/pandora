using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Assistant;
using Pottmayer.Tars.Core.Mediator.DI;

namespace Pottmayer.Pandora.Modules.Notes.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddNotesApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        services.AddScoped<IAssistantTool, CreateNoteTool>();
        services.AddScoped<IAssistantTool, SearchNotesTool>();
        services.AddScoped<IAssistantTool, ReadNoteTool>();
        services.AddScoped<IAssistantTool, AppendToNoteTool>();

        return services;
    }
}
