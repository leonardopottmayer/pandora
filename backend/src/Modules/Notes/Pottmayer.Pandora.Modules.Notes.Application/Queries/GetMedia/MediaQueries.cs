using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetMedia;

/// <summary>
/// One folder's own items (<paramref name="FolderId"/>), or the unsorted ones, or — with neither —
/// the whole library; narrowed by <paramref name="Kind"/> and a name fragment.
/// </summary>
public sealed record ListMediaInput(Guid UserId, Guid? FolderId, bool UnsortedOnly, string? Kind, string? Search);

public sealed class ListMediaQuery(ListMediaInput input) : QueryBase<ListMediaInput, IReadOnlyList<MediaItemDto>>(input);

public sealed class ListMediaQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<ListMediaQuery, IReadOnlyList<MediaItemDto>>
{
    protected override async Task<Result<IReadOnlyList<MediaItemDto>>> HandleAsync(ListMediaQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var dtos = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var items = await ctx.AcquireRepository<IMediaItemRepository>()
                .ListAsync(input.UserId, input.FolderId, input.UnsortedOnly, input.Kind, input.Search, token);
            var withThumbnail = await ctx.AcquireRepository<IMediaRenditionRepository>()
                .WithPurposeAsync([.. items.Select(i => i.Id)], RenditionPurpose.Thumbnail, token);

            IReadOnlyList<MediaItemDto> list = [.. items.Select(i => MediaItemDto.From(i, withThumbnail.Contains(i.Id)))];
            return list;
        }, cancellationToken: ct);

        return Ok(dtos);
    }
}

public sealed record GetMediaFoldersInput(Guid UserId);

/// <summary>Every folder with its own item count, plus the unsorted and total counts.</summary>
public sealed class GetMediaFoldersQuery(GetMediaFoldersInput input) : QueryBase<GetMediaFoldersInput, MediaFoldersDto>(input);

public sealed class GetMediaFoldersQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetMediaFoldersQuery, MediaFoldersDto>
{
    protected override async Task<Result<MediaFoldersDto>> HandleAsync(GetMediaFoldersQuery request, CancellationToken ct)
    {
        var userId = request.Input.UserId;
        var dto = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var folders = await ctx.AcquireRepository<IMediaFolderRepository>().GetForUserAsync(userId, token);
            var counts = await ctx.AcquireRepository<IMediaItemRepository>().CountAsync(userId, token);
            return new MediaFoldersDto(
                [.. folders.Select(f => MediaFolderDto.From(f, counts.ByFolder.GetValueOrDefault(f.Id)))],
                counts.Unsorted,
                counts.Total);
        }, cancellationToken: ct);

        return Ok(dto);
    }
}

/// <summary>An item's bytes, or a rendition's when <paramref name="Purpose"/> is set.</summary>
public sealed record GetMediaContentInput(Guid UserId, Guid MediaId, string? Purpose = null);

public sealed class GetMediaContentQuery(GetMediaContentInput input) : QueryBase<GetMediaContentInput, MediaContentDto>(input);

public sealed class GetMediaContentQueryHandler(
    IUnitOfWorkFactory factory,
    [FromKeyedServices(NotesModule.DatabaseKey)] IFileStorage fileStorage)
    : QueryHandlerBase<GetMediaContentQuery, MediaContentDto>
{
    protected override async Task<Result<MediaContentDto>> HandleAsync(GetMediaContentQuery request, CancellationToken ct)
    {
        var input = request.Input;
        // A deleted item still serves its bytes: a note may embed it, and which do is not tracked yet.
        var found = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var item = await ctx.AcquireRepository<IMediaItemRepository>().GetByIdAsync(input.MediaId, token);
            if (item is null || item.UserId != input.UserId)
                return null;
            if (input.Purpose is null)
                return new Located(item.OriginalFileName, item.ContentType, item.StorageKey);

            var rendition = await ctx.AcquireRepository<IMediaRenditionRepository>().FindAsync(item.Id, input.Purpose, token);
            return rendition is null
                ? null
                : new Located($"{Path.GetFileNameWithoutExtension(item.OriginalFileName)}.webp", rendition.ContentType, rendition.StorageKey);
        }, cancellationToken: ct);

        if (found is null)
            return Fail(MediaErrors.NotFound);

        var blob = await fileStorage.GetAsync(found.StorageKey, ct);
        return blob is null ? Fail(MediaErrors.NotFound) : Ok(new MediaContentDto(found.FileName, found.ContentType, blob.Content));
    }

    private sealed record Located(string FileName, string ContentType, string StorageKey);
}
