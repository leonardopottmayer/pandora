using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.DisconnectAccount;

public sealed record DisconnectAccountInput(Guid UserId, Guid AccountId);

public sealed class DisconnectAccountCommand(DisconnectAccountInput input)
    : CommandBase<DisconnectAccountInput, bool>(input);
