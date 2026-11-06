using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAccount;

public sealed record UpdateAccountInput(
    Guid UserId,
    Guid AccountId,
    string Name,
    string Type,
    string? Institution,
    string? Description,
    string? Color,
    string? Icon,
    int DisplayOrder,
    string? BankCode = null);

/// <summary>Edits an account's mutable fields. Currency is intentionally absent: it is fixed at creation.</summary>
[RequiresPermission(FinancesPermissions.Accounts.Update)]
public sealed class UpdateAccountCommand(UpdateAccountInput input)
    : CommandBase<UpdateAccountInput, AccountDto>(input);
