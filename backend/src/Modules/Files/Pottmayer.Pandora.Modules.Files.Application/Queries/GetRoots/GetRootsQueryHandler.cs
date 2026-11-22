using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetRoots;

/// <summary>The user's active roots on every device, with their selection marks.</summary>
public sealed class GetRootsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetRootsQuery, IReadOnlyList<RootDto>>
{
    protected override async Task<Result<IReadOnlyList<RootDto>>> HandleAsync(GetRootsQuery request, CancellationToken cancellationToken)
    {
        var roots = await factory.ExecuteAsync(FilesModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IRootRepository>().ListActiveByUserAsync(request.Input.UserId, ct),
            cancellationToken: cancellationToken);

        return Ok([.. roots.Select(RootDto.From)]);
    }
}
