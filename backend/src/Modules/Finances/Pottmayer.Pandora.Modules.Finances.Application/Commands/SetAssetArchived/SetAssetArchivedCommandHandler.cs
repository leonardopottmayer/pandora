using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAssetArchived;

public sealed class SetAssetArchivedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetAssetArchivedCommand, AssetDto>
{
    protected override async Task<Result<AssetDto>> HandleAsync(SetAssetArchivedCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var assets = ctx.AcquireRepository<IAssetRepository>();
            var asset = await assets.FindByIdForUserAsync(input.AssetId, input.UserId, token);
            if (asset is null) return Result<Asset>.Failure([AssetErrors.NotFound]);

            if (input.Archived) asset.Archive(timeProvider);
            else asset.Unarchive();
            await assets.UpdateAsync(asset, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetEvents.EntityType, asset.Id,
                input.Archived ? AssetEvents.Archived : AssetEvents.Unarchived, timeProvider.GetUtcNow(),
                new { name = asset.Name }, ct: token);
            return Result<Asset>.Success(asset);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(AssetDto.From(result.Value!));
    }
}
