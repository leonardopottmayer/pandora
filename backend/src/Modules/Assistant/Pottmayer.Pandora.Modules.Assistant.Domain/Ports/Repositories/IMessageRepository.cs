using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;

public interface IMessageRepository : IStandardRepository<Message, Guid>;
