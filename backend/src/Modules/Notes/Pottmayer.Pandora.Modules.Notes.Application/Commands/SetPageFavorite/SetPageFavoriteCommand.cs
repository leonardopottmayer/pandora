using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageFavorite;

public sealed record SetPageFavoriteInput(Guid UserId, Guid PageId, bool Favorite);

/// <summary>Marks or unmarks a page as a favorite. Idempotent: setting the current state is a no-op.</summary>
public sealed class SetPageFavoriteCommand(SetPageFavoriteInput input)
    : CommandBase<SetPageFavoriteInput, PageDto>(input);
