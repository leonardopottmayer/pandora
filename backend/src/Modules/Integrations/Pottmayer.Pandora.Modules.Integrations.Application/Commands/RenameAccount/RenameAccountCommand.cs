using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.RenameAccount;

/// <summary>Sets the user's name for one account ("Personal", "Work").</summary>
public sealed record RenameAccountInput(Guid UserId, Guid AccountId, string Label);

[RequiresPermission(IntegrationsPermissions.Accounts.Update)]
public sealed class RenameAccountCommand(RenameAccountInput input)
    : CommandBase<RenameAccountInput, bool>(input);
