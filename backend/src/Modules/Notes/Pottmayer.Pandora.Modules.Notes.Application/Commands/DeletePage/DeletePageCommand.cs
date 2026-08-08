using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.DeletePage;

public sealed record DeletePageInput(Guid UserId, Guid PageId);

/// <summary>
/// Soft-deletes a page: it keeps its row and history but drops out of every query. (Removing the
/// link edges where it is the source arrives with the wiki-link phase.)
/// </summary>
public sealed class DeletePageCommand(DeletePageInput input)
    : CommandBase<DeletePageInput, bool>(input);
