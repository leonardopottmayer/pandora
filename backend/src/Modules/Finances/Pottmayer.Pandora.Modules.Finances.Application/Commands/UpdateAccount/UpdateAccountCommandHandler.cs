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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAccount;

public sealed class UpdateAccountCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateAccountCommand, AccountDto>
{
    protected override async Task<Result<AccountDto>> HandleAsync(UpdateAccountCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(AccountErrors.InvalidName);

        if (!AccountType.IsSupported(input.Type))
            return Fail(AccountErrors.InvalidType(input.Type));

        var type = AccountType.FromValue(input.Type);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAccountRepository>();

            var account = await repo.FindByIdForUserAsync(input.AccountId, input.UserId, token);
            if (account is null)
                return Result<Account>.Failure([AccountErrors.NotFound]);

            if (account.IsArchived)
                return Result<Account>.Failure([AccountErrors.Archived]);

            // Excludes the account itself, so renaming to the same name is allowed.
            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, account.Id, token))
                return Result<Account>.Failure([AccountErrors.NameAlreadyExists]);

            // Captured before the mutation so the audit event records both sides of the change.
            var diff = new
            {
                name = new { old = account.Name, @new = input.Name.Trim() },
                type = new { old = account.Type.Value, @new = type.Value },
                institution = new { old = account.Institution, @new = input.Institution },
                description = new { old = account.Description, @new = input.Description },
                color = new { old = account.Color, @new = input.Color },
                icon = new { old = account.Icon, @new = input.Icon },
                displayOrder = new { old = account.DisplayOrder, @new = input.DisplayOrder }
            };

            account.Update(input.Name, type, input.Institution, input.Description,
                input.Color, input.Icon, input.DisplayOrder);
            await repo.UpdateAsync(account, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, AccountEvents.EntityType, account.Id, AccountEvents.Updated, now, diff, ct: token);

            return Result<Account>.Success(account);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(AccountDto.From(result.Value!));
    }
}
