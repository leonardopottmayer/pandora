using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAccount;

public sealed record CreateAccountInput(
    Guid UserId,
    string Name,
    string Type,
    string Currency,
    string? Institution,
    string? Description,
    string? Color,
    string? Icon,
    int DisplayOrder,
    decimal? OpeningBalance,
    string? BankCode = null);

/// <summary>
/// Opens a new account for the user. An optional positive opening balance is recorded as the
/// account's first posted transaction rather than a stored balance field.
/// </summary>
[RequiresPermission(FinancesPermissions.Accounts.Create)]
public sealed class CreateAccountCommand(CreateAccountInput input)
    : CommandBase<CreateAccountInput, AccountDto>(input);
