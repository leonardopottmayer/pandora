using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAsset;

public sealed class DeleteAssetCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteAssetCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteAssetCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var assets = ctx.AcquireRepository<IAssetRepository>();
            var asset = await assets.FindByIdForUserAsync(input.AssetId, input.UserId, token);
            if (asset is null) return Result<bool>.Failure([AssetErrors.NotFound]);

            // Positions are history, closed ones too: such an asset is archived, not deleted.
            if (await ctx.AcquireRepository<IPositionRepository>().AnyForAssetAsync(asset.Id, token))
                return Result<bool>.Failure([AssetErrors.HasPositions]);

            await assets.RemoveAsync(asset, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetEvents.EntityType, asset.Id, AssetEvents.Deleted,
                timeProvider.GetUtcNow(), new { name = asset.Name }, ct: token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
