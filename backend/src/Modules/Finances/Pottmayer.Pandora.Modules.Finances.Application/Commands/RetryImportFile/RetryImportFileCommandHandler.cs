using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RetryImportFile;

public sealed class RetryImportFileCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<RetryImportFileCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(
        RetryImportFileCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IImportFileRepository>();
            var file = await repo.FindByIdForUserAsync(input.ImportFileId, input.UserId, token);
            if (file is null) return Result<bool>.Failure([ImportErrors.NotFound]);

            // Only a file currently in the failed state is eligible for another attempt.
            var retried = file.Retry(timeProvider);
            if (!retried) return Result<bool>.Failure([ImportErrors.NotFailed]);

            await repo.UpdateAsync(file, token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
