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

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAssetClasses;

public sealed class GetAssetClassesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAssetClassesQuery, IReadOnlyList<AssetClassDto>>
{
    protected override async Task<Result<IReadOnlyList<AssetClassDto>>> HandleAsync(GetAssetClassesQuery request, CancellationToken ct)
    {
        var classes = await factory.ExecuteAsync(FinancesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IAssetClassRepository>().ListVisibleAsync(request.Input.UserId, token), cancellationToken: ct);
        return Ok([.. classes.Select(AssetClassDto.From)]);
    }
}
