using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAccount;

public sealed record DeleteAccountInput(Guid UserId, Guid AccountId);

public sealed class DeleteAccountCommand(DeleteAccountInput input)
    : CommandBase<DeleteAccountInput, bool>(input);
