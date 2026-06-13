using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransfer;

public sealed class CreateTransferCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateTransferCommand, IReadOnlyList<TransactionDto>>
{
    protected override async Task<Result<IReadOnlyList<TransactionDto>>> HandleAsync(
        CreateTransferCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(TransactionErrors.InvalidDescription);

        if (input.AmountOut <= 0)
            return Fail(TransactionErrors.InvalidAmount);

        if (input.FromAccountId == input.ToAccountId)
            return Fail(TransactionErrors.SameAccountTransfer);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var accounts = ctx.AcquireRepository<IAccountRepository>();

            var from = await accounts.FindByIdForUserAsync(input.FromAccountId, input.UserId, token);
            if (from is null)
                return Result<(Transaction, Transaction)>.Failure([AccountErrors.NotFound]);

            var to = await accounts.FindByIdForUserAsync(input.ToAccountId, input.UserId, token);
            if (to is null)
                return Result<(Transaction, Transaction)>.Failure([AccountErrors.NotFound]);

            if (from.IsArchived || to.IsArchived)
                return Result<(Transaction, Transaction)>.Failure([TransactionErrors.AccountArchived]);

            var sameCurrency = from.Currency.Value == to.Currency.Value;
            decimal amountIn;
            decimal? fxRate;

            if (sameCurrency)
            {
                amountIn = input.AmountOut;
                fxRate = null;
            }
            else
            {
                if (input.AmountIn is not { } providedIn || providedIn <= 0 || input.FxRate is not { } rate || rate <= 0)
                    return Result<(Transaction, Transaction)>.Failure([TransactionErrors.CrossCurrencyNeedsBothAmounts]);
                amountIn = providedIn;
                fxRate = rate;
            }

            var (outLeg, inLeg) = Transaction.CreateTransferPair(
                input.UserId, from.Id, from.Currency, input.AmountOut, to.Id, to.Currency, amountIn,
                fxRate, input.OccurredOn, input.Description, input.Notes, timeProvider);

            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            await transactions.AddAsync(outLeg, token);
            await transactions.AddAsync(inLeg, token);

            var correlationId = outLeg.TransferGroupId!.Value;
            foreach (var leg in new[] { outLeg, inLeg })
                await ctx.RecordAsync(
                    input.UserId, input.UserId, "transaction", leg.Id, "transaction.created", now,
                    new
                    {
                        accountId = leg.AccountId,
                        kind = leg.Kind.Value,
                        amount = leg.Amount,
                        currency = leg.Currency.Value,
                        transferGroupId = leg.TransferGroupId,
                        fxRate = leg.FxRate
                    },
                    correlationId, token);

            return Result<(Transaction, Transaction)>.Success((outLeg, inLeg));
        }, cancellationToken: ct);

        if (result.IsFailure)
            return Fail([.. result.Errors]);

        var (createdOut, createdIn) = result.Value;
        IReadOnlyList<TransactionDto> dtos = [TransactionDto.From(createdOut), TransactionDto.From(createdIn)];
        return Ok(dtos);
    }
}
