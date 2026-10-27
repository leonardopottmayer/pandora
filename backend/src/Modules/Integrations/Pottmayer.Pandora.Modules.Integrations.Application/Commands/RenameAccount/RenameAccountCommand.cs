using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Commands.RenameAccount;

/// <summary>Sets the user's name for one account ("Personal", "Work").</summary>
public sealed record RenameAccountInput(Guid UserId, Guid AccountId, string Label);

public sealed class RenameAccountCommand(RenameAccountInput input)
    : CommandBase<RenameAccountInput, bool>(input);
