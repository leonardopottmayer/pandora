using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

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
    int DisplayOrder);

public sealed class UpdateAccountCommand(UpdateAccountInput input)
    : CommandBase<UpdateAccountInput, AccountDto>(input);
