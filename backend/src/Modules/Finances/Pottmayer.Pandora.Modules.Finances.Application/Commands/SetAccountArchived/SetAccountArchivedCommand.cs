using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAccountArchived;

public sealed record SetAccountArchivedInput(Guid UserId, Guid AccountId, bool Archived);

public sealed class SetAccountArchivedCommand(SetAccountArchivedInput input)
    : CommandBase<SetAccountArchivedInput, AccountDto>(input);
