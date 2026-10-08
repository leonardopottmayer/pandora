using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetFilters;

/// <summary>Every filter of the user, at every scope, enabled or not. Built-in ones first.</summary>
public sealed class GetFiltersQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetFiltersQuery, IReadOnlyList<FilterDto>>
{
    protected override async Task<Result<IReadOnlyList<FilterDto>>> HandleAsync(GetFiltersQuery request, CancellationToken cancellationToken)
    {
        var filters = await factory.ExecuteAsync(FilesModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IFilterRepository>().ListByUserAsync(request.Input.UserId, ct),
            cancellationToken: cancellationToken);

        return Ok([.. filters.Select(FilterDto.From)]);
    }
}
