using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAsset;

public sealed class UpdateAssetCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateAssetCommand, AssetDto>
{
    protected override async Task<Result<AssetDto>> HandleAsync(UpdateAssetCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name)) return Fail(AssetErrors.InvalidName);

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var assets = ctx.AcquireRepository<IAssetRepository>();
            var asset = await assets.FindByIdForUserAsync(input.AssetId, input.UserId, token);
            if (asset is null) return Result<Asset>.Failure([AssetErrors.NotFound]);
            if (await assets.ExistsWithNameAsync(input.UserId, input.Name, asset.Id, token))
                return Result<Asset>.Failure([AssetErrors.NameAlreadyExists]);
            if (Asset.NormalizeCode(input.Code) is { } code && await assets.ExistsWithCodeAsync(input.UserId, asset.AssetClassId, code, asset.Id, token))
                return Result<Asset>.Failure([AssetErrors.CodeAlreadyExists]);

            var diff = new
            {
                name = new { old = asset.Name, @new = input.Name.Trim() },
                code = new { old = asset.Code, @new = Asset.NormalizeCode(input.Code) },
                isin = new { old = asset.Isin, @new = Asset.NormalizeCode(input.Isin) },
                issuer = new { old = asset.Issuer, @new = input.Issuer }
            };
            asset.Update(input.Name, input.Code, input.Isin, input.Issuer);
            await assets.UpdateAsync(asset, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetEvents.EntityType, asset.Id, AssetEvents.Updated, timeProvider.GetUtcNow(), diff, ct: token);
            return Result<Asset>.Success(asset);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(AssetDto.From(result.Value!));
    }
}
