using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.ManageMedia;

/// <summary>What can happen to one library item after its upload.</summary>
public abstract record MediaChange
{
    public sealed record Rename(string Name) : MediaChange;

    /// <summary>To a folder, or out of every folder (null, "Unsorted").</summary>
    public sealed record Move(Guid? FolderId) : MediaChange;

    public sealed record Delete : MediaChange;
}

public sealed record ChangeMediaInput(Guid UserId, Guid MediaId, MediaChange Change);

/// <summary>Renames, moves or deletes a library item. Deleting keeps the bytes (see <see cref="MediaItem.Delete"/>).</summary>
public sealed class ChangeMediaCommand(ChangeMediaInput input) : CommandBase<ChangeMediaInput, MediaItemDto>(input);

public sealed class ChangeMediaCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<ChangeMediaCommand, MediaItemDto>
{
    protected override async Task<Result<MediaItemDto>> HandleAsync(ChangeMediaCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.Change is MediaChange.Rename { Name: var name } && string.IsNullOrWhiteSpace(name))
            return Fail(MediaErrors.InvalidName);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var items = ctx.AcquireRepository<IMediaItemRepository>();
            var item = await items.FindByIdForUserAsync(input.MediaId, input.UserId, token);
            if (item is null)
                return Result<MediaItemDto>.Failure([MediaErrors.NotFound]);

            switch (input.Change)
            {
                case MediaChange.Rename rename:
                    item.Rename(rename.Name);
                    break;
                case MediaChange.Move { FolderId: { } folderId } when
                    await ctx.AcquireRepository<IMediaFolderRepository>().FindByIdForUserAsync(folderId, input.UserId, token) is null:
                    return Result<MediaItemDto>.Failure([MediaErrors.FolderNotFound]);
                case MediaChange.Move move:
                    item.MoveTo(move.FolderId);
                    break;
                case MediaChange.Delete:
                    item.Delete(timeProvider);
                    break;
            }
            await items.UpdateAsync(item, token);

            var hasThumbnail = await ctx.AcquireRepository<IMediaRenditionRepository>()
                .FindAsync(item.Id, RenditionPurpose.Thumbnail, token) is not null;
            return Result<MediaItemDto>.Success(MediaItemDto.From(item, hasThumbnail));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
