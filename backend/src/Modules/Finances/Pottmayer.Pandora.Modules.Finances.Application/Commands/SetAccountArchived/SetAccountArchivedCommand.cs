using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAccountArchived;

public sealed record SetAccountArchivedInput(Guid UserId, Guid AccountId, bool Archived);

/// <summary>Archives or unarchives an account. Idempotent: setting the current state is a no-op.</summary>
[RequiresPermission(FinancesPermissions.Accounts.Update)]
public sealed class SetAccountArchivedCommand(SetAccountArchivedInput input)
    : CommandBase<SetAccountArchivedInput, AccountDto>(input);
