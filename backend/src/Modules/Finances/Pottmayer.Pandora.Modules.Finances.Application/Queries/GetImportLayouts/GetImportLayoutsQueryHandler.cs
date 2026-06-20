using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportLayouts;

public sealed class GetImportLayoutsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetImportLayoutsQuery, IReadOnlyList<ImportLayoutDto>>
{
    protected override async Task<Result<IReadOnlyList<ImportLayoutDto>>> HandleAsync(
        GetImportLayoutsQuery request, CancellationToken ct)
    {
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IImportLayoutRepository>();
            var layouts = await repo.GetSystemLayoutsAsync(token);
            IReadOnlyList<ImportLayoutDto> dtos = layouts.Select(ImportLayoutDto.From).ToList();
            return Result<IReadOnlyList<ImportLayoutDto>>.Success(dtos);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
