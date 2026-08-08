using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageArchived;

public sealed record SetPageArchivedInput(Guid UserId, Guid PageId, bool Archived);

/// <summary>Archives or unarchives a page. Idempotent: setting the current state is a no-op.</summary>
public sealed class SetPageArchivedCommand(SetPageArchivedInput input)
    : CommandBase<SetPageArchivedInput, PageDto>(input);
