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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransferFromPending;

/// <summary>
/// Reconciles two account suggestions that are really the two legs of one transfer (an outflow on the
/// source account and an inflow on the destination). Creates the transfer pair and approves both
/// suggestions, linking each to its leg. Cards are out of scope — transfers are account-to-account.
/// </summary>
public sealed class CreateTransferFromPendingCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateTransferFromPendingCommand, IReadOnlyList<TransactionDto>>
{
    protected override async Task<Result<IReadOnlyList<TransactionDto>>> HandleAsync(
        CreateTransferFromPendingCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (input.OutflowPendingId == input.InflowPendingId)
            return Fail(PendingTransactionErrors.InvalidTransferDirections);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var pendingRepo = ctx.AcquireRepository<IPendingTransactionRepository>();
            var accountRepo = ctx.AcquireRepository<IAccountRepository>();
            var txRepo = ctx.AcquireRepository<ITransactionRepository>();

            var outflow = await pendingRepo.FindByIdForUserAsync(input.OutflowPendingId, input.UserId, token);
            var inflow = await pendingRepo.FindByIdForUserAsync(input.InflowPendingId, input.UserId, token);
            if (outflow is null || inflow is null)
                return Result<(Transaction, Transaction)>.Failure([PendingTransactionErrors.NotFound]);
            if (!outflow.IsPending || !inflow.IsPending)
                return Result<(Transaction, Transaction)>.Failure([PendingTransactionErrors.AlreadyDecided]);
            if (outflow.Amount is null or <= 0 || inflow.Amount is null or <= 0)
                return Result<(Transaction, Transaction)>.Failure([PendingTransactionErrors.MissingAmount]);

            // Both legs must target accounts (not cards), and the directions must be opposite.
            if (outflow.AccountId is null || outflow.CardId is not null ||
                inflow.AccountId is null || inflow.CardId is not null)
                return Result<(Transaction, Transaction)>.Failure([PendingTransactionErrors.NotAccountTarget]);
            if (TransactionKind.FromValue(outflow.Kind).Sign >= 0 || TransactionKind.FromValue(inflow.Kind).Sign <= 0)
                return Result<(Transaction, Transaction)>.Failure([PendingTransactionErrors.InvalidTransferDirections]);
            if (outflow.AccountId.Value == inflow.AccountId.Value)
                return Result<(Transaction, Transaction)>.Failure([PendingTransactionErrors.SameTransferAccount]);

            var from = await accountRepo.FindByIdForUserAsync(outflow.AccountId.Value, input.UserId, token);
            var to = await accountRepo.FindByIdForUserAsync(inflow.AccountId.Value, input.UserId, token);
            if (from is null || to is null)
                return Result<(Transaction, Transaction)>.Failure([AccountErrors.NotFound]);
            if (from.IsArchived || to.IsArchived)
                return Result<(Transaction, Transaction)>.Failure([TransactionErrors.AccountArchived]);

            var amountOut = outflow.Amount.Value;
            decimal amountIn;
            decimal? fxRate;
            if (from.Currency.Value == to.Currency.Value)
            {
                amountIn = amountOut;
                fxRate = null;
            }
            else
            {
                // Unlike CreateTransfer, the rate isn't supplied — it's derived from the two
                // suggestions' own amounts, since both already came from independent sources (e.g.
                // two separate bank statement imports) that recorded their own converted value.
                amountIn = inflow.Amount.Value;
                fxRate = decimal.Round(amountIn / amountOut, 8);
            }

            var description = string.IsNullOrWhiteSpace(input.Description) ? outflow.Description : input.Description.Trim();
            var occurredOn = input.OccurredOn ?? outflow.OccurredOn;

            var (outLeg, inLeg) = Transaction.CreateTransferPair(
                input.UserId, from.Id, from.Currency, amountOut, to.Id, to.Currency, amountIn,
                fxRate, occurredOn, description, notes: null, timeProvider);

            outLeg.MarkAsImport(outflow.Id);
            inLeg.MarkAsImport(inflow.Id);
            await txRepo.AddAsync(outLeg, token);
            await txRepo.AddAsync(inLeg, token);

            outflow.Approve(outLeg.Id, input.UserId, timeProvider);
            inflow.Approve(inLeg.Id, input.UserId, timeProvider);
            await pendingRepo.UpdateAsync(outflow, token);
            await pendingRepo.UpdateAsync(inflow, token);

            var correlationId = outLeg.TransferGroupId!.Value;
            foreach (var leg in new[] { outLeg, inLeg })
                await ctx.RecordAsync(
                    input.UserId, input.UserId, TransactionEvents.EntityType, leg.Id, TransactionEvents.Created, now,
                    new
                    {
                        origin = "import",
                        accountId = leg.AccountId,
                        kind = leg.Kind.Value,
                        amount = leg.Amount,
                        currency = leg.Currency.Value,
                        transferGroupId = leg.TransferGroupId,
                        fxRate = leg.FxRate
                    },
                    correlationId, token);
            foreach (var (pending, leg) in new[] { (outflow, outLeg), (inflow, inLeg) })
                await ctx.RecordAsync(input.UserId, input.UserId, PendingTransactionEvents.EntityType, pending.Id,
                    PendingTransactionEvents.Approved, now, new { transactionId = leg.Id, transferGroupId = correlationId }, correlationId, token);

            return Result<(Transaction, Transaction)>.Success((outLeg, inLeg));
        }, cancellationToken: ct);

        if (result.IsFailure) return Fail([.. result.Errors]);

        var (createdOut, createdIn) = result.Value;
        IReadOnlyList<TransactionDto> dtos = [TransactionDto.From(createdOut), TransactionDto.From(createdIn)];
        return Ok(dtos);
    }
}
