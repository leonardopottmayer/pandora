using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;

public sealed record ActivateAccountInput(string Token);

public sealed class ActivateAccountCommand(ActivateAccountInput input)
    : CommandBase<ActivateAccountInput, bool>(input);
