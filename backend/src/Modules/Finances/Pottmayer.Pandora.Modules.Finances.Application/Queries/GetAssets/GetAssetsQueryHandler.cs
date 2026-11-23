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

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAssets;

public sealed class GetAssetsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAssetsQuery, IReadOnlyList<AssetDto>>
{
    protected override async Task<Result<IReadOnlyList<AssetDto>>> HandleAsync(GetAssetsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var assets = await factory.ExecuteAsync(FinancesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IAssetRepository>().ListAsync(input.UserId, input.AssetClassId, input.Search, token), cancellationToken: ct);
        return Ok([.. assets.Select(AssetDto.From)]);
    }
}
