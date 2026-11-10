using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Infrastructure.Google;
using Pottmayer.Pandora.Modules.Agenda.Infrastructure.Jobs;

namespace Pottmayer.Pandora.Modules.Agenda.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddAgendaInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AgendaOptions>()
            .Bind(builder.Configuration.GetSection(AgendaOptions.SectionName));

        builder.Services.AddHostedService<ReminderSweepBackgroundService>();
        builder.Services.AddHostedService<TaskAlertSweepBackgroundService>();
        builder.Services.AddHostedService<EventAlertSweepBackgroundService>();

        builder.Services.AddHttpClient<IGoogleCalendarClient, GoogleCalendarClient>();
        builder.Services.AddHostedService<GoogleCalendarSyncBackgroundService>();

        return builder;
    }
}
