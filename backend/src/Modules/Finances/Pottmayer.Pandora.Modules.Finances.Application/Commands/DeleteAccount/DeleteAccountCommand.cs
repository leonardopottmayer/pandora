using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAccount;

public sealed record DeleteAccountInput(Guid UserId, Guid AccountId);

/// <summary>Permanently removes an account. Fails if it already has any transaction history.</summary>
public sealed class DeleteAccountCommand(DeleteAccountInput input)
    : CommandBase<DeleteAccountInput, bool>(input);
