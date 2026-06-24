using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UnvoidTransaction;

public sealed class UnvoidTransactionCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UnvoidTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        UnvoidTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITransactionRepository>();

            var transaction = await repo.FindByIdForUserAsync(input.TransactionId, input.UserId, token);
            if (transaction is null)
                return Result<Transaction>.Failure([TransactionErrors.NotFound]);

            if (!transaction.IsVoid)
                return Result<Transaction>.Failure([TransactionErrors.NotVoid]);

            if (transaction.InstallmentPlanId is not null)
                return await UnvoidInstallmentAsync(ctx, repo, transaction, input, now, today, token);

            // A transfer is restored as a unit: restoring one leg restores its partner in the same UoW.
            var toRestore = transaction.TransferGroupId is null
                ? [transaction]
                : await repo.GetByTransferGroupAsync(transaction.TransferGroupId.Value, input.UserId, token);

            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var correlationId = Guid.CreateVersion7();

            foreach (var entry in toRestore)
            {
                if (!entry.Restore(timeProvider)) continue;
                await repo.UpdateAsync(entry, token);

                if (entry.CardStatementId is not null)
                {
                    // Standalone card purchase/refund: reapply its effect on the statement total.
                    var statement = await statements.FindByIdForUserAsync(entry.CardStatementId.Value, input.UserId, token);
                    if (statement is not null)
                    {
                        StatementAmountSync.Apply(statement, entry.Amount * entry.Kind.StatementSign, 0m, today, timeProvider);
                        await statements.UpdateAsync(statement, token);
                    }
                }
                else if (entry.PaidStatementId is not null)
                {
                    // Statement payment: reapply its contribution to the statement's paid amount.
                    var statement = await statements.FindByIdForUserAsync(entry.PaidStatementId.Value, input.UserId, token);
                    if (statement is not null)
                    {
                        StatementAmountSync.Apply(statement, 0m, entry.Amount, today, timeProvider);
                        await statements.UpdateAsync(statement, token);
                    }
                }

                await ctx.RecordAsync(
                    input.UserId, input.UserId, TransactionEvents.EntityType, entry.Id, TransactionEvents.Restored, now,
                    data: null, correlationId, token);
            }

            return Result<Transaction>.Success(transaction);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(TransactionDto.From(result.Value!));
    }

    /// <summary>
    /// Restores either a single voided installment or every voided installment of the plan. Only
    /// entries that are actually <c>void</c> are touched, mirroring whichever ones the original void
    /// (single or whole-plan) effectively cancelled.
    /// </summary>
    private async Task<Result<Transaction>> UnvoidInstallmentAsync(
        IDataContext ctx,
        ITransactionRepository repo,
        Transaction transaction,
        UnvoidTransactionInput input,
        DateTimeOffset now,
        DateOnly today,
        CancellationToken ct)
    {
        var statements = ctx.AcquireRepository<ICardStatementRepository>();
        var correlationId = Guid.CreateVersion7();

        var targets = input.UnvoidEntirePlan
            ? await repo.GetByInstallmentPlanAsync(transaction.InstallmentPlanId!.Value, input.UserId, ct)
            : [transaction];

        foreach (var entry in targets)
        {
            if (!entry.IsVoid || entry.CardStatementId is null) continue;

            var statement = await statements.FindByIdForUserAsync(entry.CardStatementId.Value, input.UserId, ct);
            if (statement is null) continue;

            if (!entry.Restore(timeProvider)) continue;
            await repo.UpdateAsync(entry, ct);

            StatementAmountSync.Apply(statement, entry.Amount, 0m, today, timeProvider);
            await statements.UpdateAsync(statement, ct);

            await ctx.RecordAsync(
                input.UserId, input.UserId, TransactionEvents.EntityType, entry.Id, TransactionEvents.Restored, now,
                new { installmentNumber = entry.InstallmentNumber }, correlationId, ct);
        }

        if (input.UnvoidEntirePlan)
            await ctx.RecordAsync(
                input.UserId, input.UserId, InstallmentPlanEvents.EntityType, transaction.InstallmentPlanId!.Value,
                InstallmentPlanEvents.Restored, now, ct: ct);

        return Result<Transaction>.Success(transaction);
    }
}
