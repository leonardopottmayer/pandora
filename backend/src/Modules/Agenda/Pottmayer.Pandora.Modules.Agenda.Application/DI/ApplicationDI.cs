using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Agenda.Application.Assistant;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Integrations;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddAgendaApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        // Integration-event subscribers (dispatched by the in-process IIntegrationEventBus).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        // The Agenda's contribution to the assistant tool catalog.
        services.AddScoped<IAssistantTool, CreateReminderTool>();
        services.AddScoped<IAssistantTool, CreateTaskTool>();
        services.AddScoped<IAssistantTool, CreateEventTool>();
        services.AddScoped<IAssistantTool, CompleteTaskTool>();
        services.AddScoped<IAssistantTool, RescheduleReminderTool>();
        services.AddScoped<IAssistantTool, ListAgendaTool>();
        services.AddScoped<IAssistantTool, ListTasksTool>();
        services.AddScoped<IAssistantTool, UpdateTaskTool>();
        services.AddScoped<IAssistantTool, ReopenTaskTool>();
        services.AddScoped<IAssistantTool, DeleteTaskTool>();
        services.AddScoped<IAssistantTool, UpdateEventTool>();
        services.AddScoped<IAssistantTool, DeleteEventTool>();
        services.AddScoped<IAssistantTool, CancelReminderTool>();
        services.AddScoped<IAssistantTool, RenameReminderTool>();
        services.AddScoped<IAssistantTool, ListRemindersTool>();

        // Google Calendar and Tasks sync (the HTTP clients are Infrastructure's); the bound calendars and lists as account usages.
        services.AddSingleton<CalendarSyncGates>();
        services.AddScoped<GoogleCalendarSync>();
        services.AddScoped<GoogleTasksSync>();
        services.AddScoped<IExternalAccountUsageSource, AgendaAccountUsageSource>();

        // What an administrator can switch off and grant in this module (see the Admin module).
        services.AddTarsFeatureFlagDefinitions(f => f.Add(AgendaFeatures.Agenda));
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(AgendaPermissions.Module, feature: AgendaFeatures.Agenda)
                .Resource(AgendaPermissions.Calendars.Resource).Crud()
                .Resource(AgendaPermissions.Events.Resource).Crud()
                .Resource(AgendaPermissions.Reminders.Resource).Crud()
                .Resource(AgendaPermissions.Lists.Resource).Crud()
                .Resource(AgendaPermissions.Tasks.Resource).Crud()
                .Resource(AgendaPermissions.Alerts.Resource).Read().Create().Delete()
                .Resource(AgendaPermissions.Preferences.Resource).Read().Update());

        return services;
    }
}
