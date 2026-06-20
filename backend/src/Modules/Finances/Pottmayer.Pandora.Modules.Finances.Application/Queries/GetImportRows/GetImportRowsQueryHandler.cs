using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportRows;

public sealed class GetImportRowsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetImportRowsQuery, IReadOnlyList<ImportRowDto>>
{
    protected override async Task<Result<IReadOnlyList<ImportRowDto>>> HandleAsync(
        GetImportRowsQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            // Verify the file belongs to the user before returning rows
            var fileRepo = ctx.AcquireRepository<IImportFileRepository>();
            var file = await fileRepo.FindByIdForUserAsync(input.ImportFileId, input.UserId, token);
            if (file is null) return Result<IReadOnlyList<ImportRowDto>>.Failure([ImportErrors.NotFound]);

            var rowRepo = ctx.AcquireRepository<IImportRowRepository>();
            var rows = await rowRepo.GetByImportFileAsync(input.ImportFileId, token);
            IReadOnlyList<ImportRowDto> dtos = rows.Select(ImportRowDto.From).ToList();
            return Result<IReadOnlyList<ImportRowDto>>.Success(dtos);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
