using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportFile;

public sealed class GetImportFileQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetImportFileQuery, ImportFileDto>
{
    protected override async Task<Result<ImportFileDto>> HandleAsync(
        GetImportFileQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IImportFileRepository>();
            var file = await repo.FindByIdForUserAsync(input.ImportFileId, input.UserId, token);
            if (file is null) return Result<ImportFileDto>.Failure([ImportErrors.NotFound]);
            return Result<ImportFileDto>.Success(ImportFileDto.From(file));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
