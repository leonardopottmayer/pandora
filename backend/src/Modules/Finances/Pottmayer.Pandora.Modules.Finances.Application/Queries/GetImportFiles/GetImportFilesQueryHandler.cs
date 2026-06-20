using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFiles;

public sealed class GetImportFilesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetImportFilesQuery, IReadOnlyList<ImportFileDto>>
{
    protected override async Task<Result<IReadOnlyList<ImportFileDto>>> HandleAsync(
        GetImportFilesQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IImportFileRepository>();
            var items = await repo.QueryAsync(input.UserId, input.Filter, token);
            IReadOnlyList<ImportFileDto> dtos = items.Select(ImportFileDto.From).ToList();
            return Result<IReadOnlyList<ImportFileDto>>.Success(dtos);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
