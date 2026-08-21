using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;

/// <summary>
/// Registered inline buttons. A callback resolves one by its id (the callback_data), which is why the
/// standard key lookup is all the ingress path needs.
/// </summary>
public interface IInteractionRepository : IStandardRepository<Interaction, Guid>;
