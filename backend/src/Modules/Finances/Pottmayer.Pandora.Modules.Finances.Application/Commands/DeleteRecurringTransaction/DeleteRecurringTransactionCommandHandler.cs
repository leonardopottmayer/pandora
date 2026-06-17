using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteRecurringTransaction;

public sealed class DeleteRecurringTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<DeleteRecurringTransactionCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(
        DeleteRecurringTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var recurring = await repo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (recurring is null) return Result<bool>.Failure([RecurringTransactionErrors.NotFound]);

            await repo.RemoveAsync(recurring, token);
            await ctx.RecordAsync(input.UserId, input.UserId, "recurring-transaction", recurring.Id,
                "recurring.deleted", now, ct: token);

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
