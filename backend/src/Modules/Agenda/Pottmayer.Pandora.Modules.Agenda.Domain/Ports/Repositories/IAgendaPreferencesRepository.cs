using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

/// <summary>Keyed by the user id.</summary>
public interface IAgendaPreferencesRepository : IStandardRepository<AgendaPreferences, Guid>;
