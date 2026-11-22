using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.DisconnectAccount;

public sealed record DisconnectAccountInput(Guid UserId, Guid AccountId);

[RequiresPermission(IntegrationsPermissions.Accounts.Delete)]
public sealed class DisconnectAccountCommand(DisconnectAccountInput input)
    : CommandBase<DisconnectAccountInput, bool>(input);
