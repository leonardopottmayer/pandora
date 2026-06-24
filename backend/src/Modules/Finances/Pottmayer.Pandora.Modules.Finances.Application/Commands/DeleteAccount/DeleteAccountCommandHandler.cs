using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAccount;

public sealed class DeleteAccountCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteAccountCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteAccountCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAccountRepository>();

            var account = await repo.FindByIdForUserAsync(input.AccountId, input.UserId, token);
            if (account is null)
                return Result<bool>.Failure([AccountErrors.NotFound]);

            // A hard delete is only safe for an account with no ledger history; otherwise archiving
            // is the right tool — checking for a single existing transaction is enough to tell.
            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var existingTransactions = await transactions.QueryAsync(
                input.UserId, new TransactionFilter(AccountId: account.Id, Take: 1), token);
            if (existingTransactions.Count > 0)
                return Result<bool>.Failure([AccountErrors.HasHistory]);

            await repo.RemoveAsync(account, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, AccountEvents.EntityType, account.Id, AccountEvents.Deleted, now,
                new { name = account.Name }, ct: token);

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
