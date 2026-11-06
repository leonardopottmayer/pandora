using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

public interface IAuditEntryRepository : IStandardRepository<AuditEntry, Guid>;
