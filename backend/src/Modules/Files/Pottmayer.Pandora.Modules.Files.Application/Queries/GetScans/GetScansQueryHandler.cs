using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetScans;

/// <summary>The root's latest scans, newest first — including a held one waiting for the user.</summary>
public sealed class GetScansQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetScansQuery, IReadOnlyList<ScanDto>>
{
    private const int Limit = 50;

    protected override async Task<Result<IReadOnlyList<ScanDto>>> HandleAsync(GetScansQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;

        return await factory.ExecuteAsync<Result<IReadOnlyList<ScanDto>>>(FilesModule.DatabaseKey, async (ctx, ct) =>
        {
            var root = await ctx.AcquireRepository<IRootRepository>().FindForUserAsync(input.RootId, input.UserId, ct);
            if (root is null) return FilesErrors.RootNotFound;

            var scans = await ctx.AcquireRepository<IScanRepository>().ListByRootAsync(root.Id, Limit, ct);
            return Result<IReadOnlyList<ScanDto>>.Success([.. scans.Select(ScanDto.From)]);
        }, cancellationToken: cancellationToken);
    }
}
