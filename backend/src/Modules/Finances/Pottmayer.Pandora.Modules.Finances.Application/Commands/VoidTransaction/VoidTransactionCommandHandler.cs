using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.VoidTransaction;

public sealed class VoidTransactionCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<VoidTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        VoidTransactionCommand request, CancellationToken ct)
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

            if (transaction.IsVoid)
                return Result<Transaction>.Failure([TransactionErrors.AlreadyVoid]);

            if (transaction.InstallmentPlanId is not null)
                return await VoidInstallmentAsync(ctx, repo, transaction, input, now, today, token);

            // A transfer is voided as a unit: cancelling one leg cancels its partner in the same UoW.
            var toVoid = transaction.TransferGroupId is null
                ? [transaction]
                : await repo.GetByTransferGroupAsync(transaction.TransferGroupId.Value, input.UserId, token);

            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var correlationId = Guid.CreateVersion7();

            foreach (var entry in toVoid)
            {
                if (!entry.Void(input.Reason, timeProvider)) continue;
                await repo.UpdateAsync(entry, token);

                if (entry.CardStatementId is not null)
                {
                    // Standalone card purchase/refund: undo its effect on the statement total.
                    var statement = await statements.FindByIdForUserAsync(entry.CardStatementId.Value, input.UserId, token);
                    if (statement is not null)
                    {
                        StatementAmountSync.Apply(statement, -(entry.Amount * entry.Kind.StatementSign), 0m, today, timeProvider);
                        await statements.UpdateAsync(statement, token);
                    }
                }
                else if (entry.PaidStatementId is not null)
                {
                    // Statement payment: undo its contribution to the statement's paid amount.
                    var statement = await statements.FindByIdForUserAsync(entry.PaidStatementId.Value, input.UserId, token);
                    if (statement is not null)
                    {
                        StatementAmountSync.Apply(statement, 0m, -entry.Amount, today, timeProvider);
                        await statements.UpdateAsync(statement, token);
                    }
                }

                await ctx.RecordAsync(
                    input.UserId, input.UserId, TransactionEvents.EntityType, entry.Id, TransactionEvents.Voided, now,
                    new { reason = input.Reason }, correlationId, token);
            }

            return Result<Transaction>.Success(transaction);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(TransactionDto.From(result.Value!));
    }

    /// <summary>
    /// Voids either a single installment or the whole plan. An installment whose statement is no
    /// longer open has already been billed and cannot be cancelled: a single void errors, while a
    /// plan void simply leaves those installments in place and cancels the still-open ones. Each
    /// affected statement total is adjusted in the same database transaction.
    /// </summary>
    private async Task<Result<Transaction>> VoidInstallmentAsync(
        IDataContext ctx,
        ITransactionRepository repo,
        Transaction transaction,
        VoidTransactionInput input,
        DateTimeOffset now,
        DateOnly today,
        CancellationToken ct)
    {
        var statements = ctx.AcquireRepository<ICardStatementRepository>();
        var correlationId = Guid.CreateVersion7();

        var targets = input.VoidEntirePlan
            ? await repo.GetByInstallmentPlanAsync(transaction.InstallmentPlanId!.Value, input.UserId, ct)
            : [transaction];

        if (!input.VoidEntirePlan)
        {
            var statement = await statements.FindByIdForUserAsync(transaction.CardStatementId!.Value, input.UserId, ct);
            if (statement is null)
                return Result<Transaction>.Failure([StatementErrors.NotFound]);
            if (statement.Status != StatementStatus.Open)
                return Result<Transaction>.Failure([InstallmentErrors.InstallmentInClosedStatement]);
        }

        foreach (var entry in targets)
        {
            if (entry.IsVoid || entry.CardStatementId is null) continue;

            var statement = await statements.FindByIdForUserAsync(entry.CardStatementId.Value, input.UserId, ct);
            if (statement is null) continue;

            // Billed installments (statement already closed/paid) stay; only open ones are cancellable.
            if (statement.Status != StatementStatus.Open) continue;

            if (!entry.Void(input.Reason, timeProvider)) continue;
            await repo.UpdateAsync(entry, ct);

            StatementAmountSync.Apply(statement, -entry.Amount, 0m, today, timeProvider);
            await statements.UpdateAsync(statement, ct);

            await ctx.RecordAsync(
                input.UserId, input.UserId, TransactionEvents.EntityType, entry.Id, TransactionEvents.Voided, now,
                new { reason = input.Reason, installmentNumber = entry.InstallmentNumber }, correlationId, ct);
        }

        if (input.VoidEntirePlan)
            await ctx.RecordAsync(
                input.UserId, input.UserId, InstallmentPlanEvents.EntityType, transaction.InstallmentPlanId!.Value,
                InstallmentPlanEvents.Voided, now, new { reason = input.Reason }, correlationId, ct);

        return Result<Transaction>.Success(transaction);
    }
}
