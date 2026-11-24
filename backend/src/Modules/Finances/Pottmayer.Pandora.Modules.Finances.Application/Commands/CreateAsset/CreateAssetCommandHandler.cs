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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAsset;

public sealed class CreateAssetCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateAssetCommand, AssetDto>
{
    protected override async Task<Result<AssetDto>> HandleAsync(CreateAssetCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Name)) return Fail(AssetErrors.InvalidName);
        if (!CurrencyCode.TryCreate(input.Currency, out var currency)) return Fail(AccountErrors.InvalidCurrency(input.Currency));

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var assetClass = await AssetClassLookup.FindAsync(ctx, input.AssetClassId, input.UserId, token);
            if (assetClass is null) return Result<Asset>.Failure([AssetErrors.ClassNotFound]);
            if (assetClass.IsArchived) return Result<Asset>.Failure([AssetErrors.ClassArchived]);

            var assets = ctx.AcquireRepository<IAssetRepository>();
            if (await assets.ExistsWithNameAsync(input.UserId, input.Name, null, token))
                return Result<Asset>.Failure([AssetErrors.NameAlreadyExists]);
            if (Asset.NormalizeCode(input.Code) is { } code && await assets.ExistsWithCodeAsync(input.UserId, assetClass.Ref.Id, code, null, token))
                return Result<Asset>.Failure([AssetErrors.CodeAlreadyExists]);

            var asset = Asset.Create(input.UserId, assetClass.Ref, input.Name, input.Code, input.Isin, input.Issuer, currency!, timeProvider);
            await assets.AddAsync(asset, token);
            await ctx.RecordAsync(input.UserId, input.UserId, AssetEvents.EntityType, asset.Id, AssetEvents.Created, timeProvider.GetUtcNow(),
                new { name = asset.Name, code = asset.Code, assetClassId = asset.AssetClassId, currency = asset.Currency.Value }, ct: token);
            return Result<Asset>.Success(asset);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(AssetDto.From(result.Value!));
    }
}
