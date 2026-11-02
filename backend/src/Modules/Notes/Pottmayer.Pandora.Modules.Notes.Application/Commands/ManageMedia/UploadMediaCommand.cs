using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.ManageMedia;

public sealed record UploadMediaInput(Guid UserId, Guid? FolderId, string FileName, string ContentType, byte[] Content);

/// <summary>
/// Adds a file to the media library, in a folder or unsorted. An image is measured and, when larger
/// than <see cref="RenditionPurpose.ThumbnailMaxSide"/>, gets a thumbnail; one the server cannot read
/// is kept as a plain file. Kinds the library does not take yet (video) are refused.
/// </summary>
public sealed class UploadMediaCommand(UploadMediaInput input) : CommandBase<UploadMediaInput, MediaItemDto>(input);

public sealed class UploadMediaCommandHandler(
    IUnitOfWorkFactory factory,
    [FromKeyedServices(NotesModule.DatabaseKey)] IFileStorage fileStorage,
    IImageProcessor images,
    TimeProvider timeProvider)
    : CommandHandlerBase<UploadMediaCommand, MediaItemDto>
{
    /// <summary>The database blob store's ceiling; a bucket backend is what would lift it.</summary>
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;

    protected override async Task<Result<MediaItemDto>> HandleAsync(UploadMediaCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.Content.Length == 0)
            return Fail(MediaErrors.Empty);
        if (input.Content.Length > MaxFileSizeBytes)
            return Fail(MediaErrors.TooLarge);

        var kind = MediaKind.FromContentType(input.ContentType);
        if (!MediaKind.Accepted.Contains(kind))
            return Fail(MediaErrors.UnsupportedKind);

        ImageSize? info = null;
        EncodedImage? thumbnail = null;
        if (kind == MediaKind.Image)
        {
            info = images.Inspect(input.Content);
            if (info is null)
                kind = MediaKind.File;
            else if (Math.Max(info.Width, info.Height) > RenditionPurpose.ThumbnailMaxSide)
                thumbnail = images.Thumbnail(input.Content, RenditionPurpose.ThumbnailMaxSide);
        }

        if (input.FolderId is { } folderId)
        {
            var folderExists = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
                await ctx.AcquireRepository<IMediaFolderRepository>().FindByIdForUserAsync(folderId, input.UserId, token) is not null,
                cancellationToken: ct);
            if (!folderExists)
                return Fail(MediaErrors.FolderNotFound);
        }

        // The bytes go first (like object storage: the blob write and its metadata row commit
        // separately), then the rows that point at them.
        var fileName = string.IsNullOrWhiteSpace(input.FileName) ? "file" : input.FileName.Trim();
        var storageKey = await fileStorage.SaveAsync(fileName, input.ContentType, input.Content, ct);
        var thumbnailKey = thumbnail is null
            ? null
            : await fileStorage.SaveAsync($"{fileName}.webp", thumbnail.ContentType, thumbnail.Content, ct);

        var item = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var created = MediaItem.Create(
                input.UserId, input.FolderId, kind, fileName, input.ContentType, input.Content.Length,
                info?.Width, info?.Height, fileStorage.Backend, storageKey, timeProvider);
            await ctx.AcquireRepository<IMediaItemRepository>().AddAsync(created, token);

            if (thumbnail is not null)
                await ctx.AcquireRepository<IMediaRenditionRepository>().AddAsync(MediaRendition.Create(
                    created.Id, RenditionPurpose.Thumbnail, thumbnail.Width, thumbnail.Height, thumbnail.ContentType,
                    thumbnail.Content.Length, fileStorage.Backend, thumbnailKey!, timeProvider), token);

            return created;
        }, cancellationToken: ct);

        return Ok(MediaItemDto.From(item, hasThumbnail: thumbnail is not null));
    }
}
