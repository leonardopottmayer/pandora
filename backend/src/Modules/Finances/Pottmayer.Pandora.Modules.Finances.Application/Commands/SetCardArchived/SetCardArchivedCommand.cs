using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetCardArchived;

public sealed record SetCardArchivedInput(Guid UserId, Guid CardId, bool Archived);

public sealed class SetCardArchivedCommand(SetCardArchivedInput input) : CommandBase<SetCardArchivedInput, CardDto>(input);
