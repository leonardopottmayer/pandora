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
/// the whole library; narrowed by <paramref name="Kind"/>, a name fragment, and to the items no page
/// shows (<paramref name="UnusedOnly"/>).
/// </summary>
public sealed record ListMediaInput(
    Guid UserId, Guid? FolderId, bool UnsortedOnly, string? Kind, string? Search, bool UnusedOnly = false);

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
            var ids = items.Select(i => i.Id).ToList();
            var withThumbnail = await ctx.AcquireRepository<IMediaRenditionRepository>()
                .WithPurposeAsync(ids, RenditionPurpose.Thumbnail, token);
            var usages = await ctx.AcquireRepository<IPageMediaRepository>().CountByMediaAsync(ids, token);

            IReadOnlyList<MediaItemDto> list =
            [
                .. items
                    .Where(i => !input.UnusedOnly || !usages.ContainsKey(i.Id))
                    .Select(i => MediaItemDto.From(i, withThumbnail.Contains(i.Id), usages.GetValueOrDefault(i.Id)))
            ];
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
            var items = ctx.AcquireRepository<IMediaItemRepository>();
            var counts = await items.CountAsync(userId, token);

            // A personal library is small enough to read whole for this count.
            var ids = (await items.ListAsync(userId, null, false, null, null, token)).Select(i => i.Id).ToList();
            var used = await ctx.AcquireRepository<IPageMediaRepository>().CountByMediaAsync(ids, token);

            return new MediaFoldersDto(
                [.. folders.Select(f => MediaFolderDto.From(f, counts.ByFolder.GetValueOrDefault(f.Id)))],
                counts.Unsorted,
                ids.Count(id => !used.ContainsKey(id)),
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

public sealed record GetMediaUsagesInput(Guid UserId, Guid MediaId);

/// <summary>The pages that show an item — what the library lists as "used in".</summary>
public sealed class GetMediaUsagesQuery(GetMediaUsagesInput input) : QueryBase<GetMediaUsagesInput, IReadOnlyList<MediaUsageDto>>(input);

public sealed class GetMediaUsagesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetMediaUsagesQuery, IReadOnlyList<MediaUsageDto>>
{
    protected override async Task<Result<IReadOnlyList<MediaUsageDto>>> HandleAsync(GetMediaUsagesQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var usages = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var owned = await ctx.AcquireRepository<IMediaItemRepository>().GetByIdsForUserAsync([input.MediaId], input.UserId, token);
            if (owned.Count == 0)
                return null;

            var pageIds = await ctx.AcquireRepository<IPageMediaRepository>().GetPageIdsByMediaAsync(input.MediaId, token);
            var pages = await ctx.AcquireRepository<IPageRepository>().GetByIdsForUserAsync(pageIds, input.UserId, token);
            IReadOnlyList<MediaUsageDto> list = [.. pages.Select(p => new MediaUsageDto(p.Id, p.SpaceId, p.Title, p.Icon, p.IsArchived))];
            return list;
        }, cancellationToken: ct);

        return usages is null ? Fail(MediaErrors.NotFound) : Ok(usages);
    }
}

public sealed record LookupMediaInput(Guid UserId, IReadOnlyCollection<Guid> Ids);

/// <summary>
/// The items a page shows, by id, deleted ones included — what the preview needs to draw a file as a
/// card (name, size, kind). Ids that are not the user's are left out.
/// </summary>
public sealed class LookupMediaQuery(LookupMediaInput input) : QueryBase<LookupMediaInput, IReadOnlyList<MediaItemDto>>(input);

public sealed class LookupMediaQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<LookupMediaQuery, IReadOnlyList<MediaItemDto>>
{
    /// <summary>More than a page would ever embed; caps what one request can ask for.</summary>
    private const int MaxIds = 200;

    protected override async Task<Result<IReadOnlyList<MediaItemDto>>> HandleAsync(LookupMediaQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var ids = input.Ids.Distinct().Take(MaxIds).ToList();
        var dtos = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var items = await ctx.AcquireRepository<IMediaItemRepository>().GetByIdsForUserAsync(ids, input.UserId, token);
            var withThumbnail = await ctx.AcquireRepository<IMediaRenditionRepository>()
                .WithPurposeAsync([.. items.Select(i => i.Id)], RenditionPurpose.Thumbnail, token);
            IReadOnlyList<MediaItemDto> list = [.. items.Select(i => MediaItemDto.From(i, withThumbnail.Contains(i.Id)))];
            return list;
        }, cancellationToken: ct);

        return Ok(dtos);
    }
}
