using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;

/// <summary>Keyed by the user id.</summary>
public interface IPreferencesRepository : IStandardRepository<Preferences, Guid>;
