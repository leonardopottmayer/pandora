using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Integrations;

/// <summary>Each bound calendar uses one Google account: "Agenda · Work" on the connections screen.</summary>
public sealed class AgendaAccountUsageSource(IUnitOfWorkFactory factory) : IExternalAccountUsageSource
{
    public const string CalendarPurpose = "calendar";

    public async Task<IReadOnlyList<ExternalAccountUsage>> ListAsync(Guid userId, CancellationToken ct = default) =>
        await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var bindings = await context.AcquireRepository<ICalendarBindingRepository>().GetByUserAsync(userId, token);
            var names = (await context.AcquireRepository<ICalendarRepository>().GetByUserAsync(userId, token))
                .ToDictionary(c => c.Id, c => c.Name);

            IReadOnlyList<ExternalAccountUsage> usages =
            [
                .. bindings.Select(b => new ExternalAccountUsage(
                    b.ExternalAccountId, AgendaModule.Name, CalendarPurpose, names.GetValueOrDefault(b.CalendarId, b.RemoteCalendarId)))
            ];
            return usages;
        }, cancellationToken: ct);
}
