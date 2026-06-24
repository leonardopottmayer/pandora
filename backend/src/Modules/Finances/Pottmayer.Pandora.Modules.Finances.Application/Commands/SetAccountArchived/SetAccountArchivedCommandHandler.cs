using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAccountArchived;

public sealed class SetAccountArchivedCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetAccountArchivedCommand, AccountDto>
{
    protected override async Task<Result<AccountDto>> HandleAsync(
        SetAccountArchivedCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAccountRepository>();

            var account = await repo.FindByIdForUserAsync(input.AccountId, input.UserId, token);
            if (account is null)
                return Result<Account>.Failure([AccountErrors.NotFound]);

            if (account.IsArchived == input.Archived)
                return Result<Account>.Success(account); // idempotent: no change, no event

            if (input.Archived)
                account.Archive(timeProvider);
            else
                account.Unarchive();

            await repo.UpdateAsync(account, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, AccountEvents.EntityType, account.Id,
                input.Archived ? AccountEvents.Archived : AccountEvents.Unarchived, now, ct: token);

            return Result<Account>.Success(account);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(AccountDto.From(result.Value!));
    }
}
