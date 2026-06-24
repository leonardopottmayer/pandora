using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateRecurringTransaction;

public sealed class UpdateRecurringTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<UpdateRecurringTransactionCommand, RecurringTransactionDto>
{
    protected override async Task<Result<RecurringTransactionDto>> HandleAsync(
        UpdateRecurringTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(RecurringTransactionErrors.MissingName);
        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(RecurringTransactionErrors.MissingDescription);
        if (input.AutoPost && input.Amount is null)
            return Fail(RecurringTransactionErrors.AutoPostRequiresAmount);

        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var recurring = await repo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (recurring is null) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.NotFound]);
            // A finished template is terminal — editing it further makes no sense once it stopped generating.
            if (recurring.IsFinished) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.Finished]);
            if (input.EndDate.HasValue && input.EndDate.Value <= recurring.StartDate)
                return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.EndDateBeforeStart]);

            recurring.UpdateTemplate(
                input.Name,
                input.Amount,
                input.AmountIsEstimate,
                input.Description,
                input.Payee,
                input.SystemCategoryId,
                input.UserCategoryId,
                input.EndDate,
                input.MaxOccurrences,
                input.AutoPost,
                input.AutoGenerate);

            await repo.UpdateAsync(recurring, token);
            await ctx.RecordAsync(input.UserId, input.UserId, RecurringTransactionEvents.EntityType, recurring.Id,
                RecurringTransactionEvents.Updated, now, new
                {
                    recurring.Name,
                    recurring.Amount,
                    recurring.Description,
                    recurring.EndDate,
                    recurring.AutoPost
                }, ct: token);

            return Result<Domain.Aggregates.RecurringTransaction>.Success(recurring);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(RecurringTransactionDto.From(result.Value!));
    }
}
