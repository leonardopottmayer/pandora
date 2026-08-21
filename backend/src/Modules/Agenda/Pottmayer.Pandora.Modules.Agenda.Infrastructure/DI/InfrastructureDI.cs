using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
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

        return builder;
    }
}
