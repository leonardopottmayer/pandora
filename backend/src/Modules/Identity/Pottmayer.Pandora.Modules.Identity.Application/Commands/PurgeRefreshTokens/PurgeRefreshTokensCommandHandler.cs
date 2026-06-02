using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.PurgeRefreshTokens;

public sealed class PurgeRefreshTokensCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<PurgeRefreshTokensCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(PurgeRefreshTokensCommand request, CancellationToken ct)
    {
        var purged = await factory.ExecuteAsync(IdentityModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRefreshTokenRepository>();
            return await repo.PurgeOldTokensAsync(
                request.Input.ConsumedOlderThan,
                request.Input.ExpiredOlderThan,
                token);
        }, cancellationToken: ct);

        return Ok(purged);
    }
}
