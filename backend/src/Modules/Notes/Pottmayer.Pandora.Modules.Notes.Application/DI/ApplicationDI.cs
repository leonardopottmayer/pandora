using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Assistant;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

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

        // What an administrator can switch off and grant in this module (see the Admin module).
        services.AddTarsFeatureFlagDefinitions(f => f.Add(NotesFeatures.Notes));
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(NotesPermissions.Module, feature: NotesFeatures.Notes)
                .Resource(NotesPermissions.Spaces.Resource).Crud()
                .Resource(NotesPermissions.Pages.Resource).Crud()
                .Resource(NotesPermissions.Media.Resource).Crud()
                .Resource(NotesPermissions.Tags.Resource).Read().Update());

        return services;
    }
}
