using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransaction;

public sealed class CreateTransactionCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        CreateTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(TransactionErrors.InvalidDescription);

        if (input.Amount <= 0)
            return Fail(TransactionErrors.InvalidAmount);

        if (!TransactionKind.IsSupported(input.Kind))
            return Fail(TransactionErrors.InvalidKind(input.Kind));

        var kind = TransactionKind.FromValue(input.Kind);
        if (kind.IsTransferLeg)
            return Fail(TransactionErrors.TransferLegNotAllowed);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var accounts = ctx.AcquireRepository<IAccountRepository>();

            var account = await accounts.FindByIdForUserAsync(input.AccountId, input.UserId, token);
            if (account is null)
                return Result<Transaction>.Failure([AccountErrors.NotFound]);

            if (account.IsArchived)
                return Result<Transaction>.Failure([TransactionErrors.AccountArchived]);

            if (kind.RequiresInvestmentAccount && account.Type != AccountType.Investment)
                return Result<Transaction>.Failure([TransactionErrors.KindRequiresInvestmentAccount(kind.Value)]);

            // Scheduled (future-dated) entries are created pending; they post on their date (phase 08 job).
            var post = input.OccurredOn <= today;

            var transaction = Transaction.Create(
                input.UserId, account.Id, kind, account.Currency, input.Amount, input.OccurredOn,
                input.Description, input.Payee, input.Notes, input.SystemCategoryId, input.UserCategoryId,
                post, timeProvider);

            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            await transactions.AddAsync(transaction, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, "transaction", transaction.Id, "transaction.created", now,
                new
                {
                    accountId = transaction.AccountId,
                    kind = transaction.Kind.Value,
                    status = transaction.Status.Value,
                    amount = transaction.Amount,
                    currency = transaction.Currency.Value,
                    occurredOn = transaction.OccurredOn
                },
                ct: token);

            return Result<Transaction>.Success(transaction);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(TransactionDto.From(result.Value!));
    }
}
