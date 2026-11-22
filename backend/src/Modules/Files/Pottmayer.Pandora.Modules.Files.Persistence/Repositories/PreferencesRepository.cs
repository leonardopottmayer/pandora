using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Persistence.Repositories;

public sealed class PreferencesRepository(IDataContextAccessor accessor)
    : StandardRepository<Preferences, Guid>(accessor), IPreferencesRepository;
