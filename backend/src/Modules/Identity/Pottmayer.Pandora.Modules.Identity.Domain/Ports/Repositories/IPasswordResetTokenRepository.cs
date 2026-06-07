using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IPasswordResetTokenRepository : IStandardRepository<PasswordResetToken, Guid>
{
    Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
