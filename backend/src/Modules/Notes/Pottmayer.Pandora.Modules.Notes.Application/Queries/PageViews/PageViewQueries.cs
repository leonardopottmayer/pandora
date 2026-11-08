using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.PageViews;

// ---------- Recent pages ----------

public sealed record GetRecentPagesInput(Guid UserId, int Limit = 20);

/// <summary>The pages the user opened last, newest first.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetRecentPagesQuery(GetRecentPagesInput input) : QueryBase<GetRecentPagesInput, IReadOnlyList<PageSummaryDto>>(input);

public sealed class GetRecentPagesQueryHandler(IUnitOfWorkFactory factory) : QueryHandlerBase<GetRecentPagesQuery, IReadOnlyList<PageSummaryDto>>
{
    protected override async Task<Result<IReadOnlyList<PageSummaryDto>>> HandleAsync(GetRecentPagesQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var pages = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var recent = await ctx.AcquireRepository<IPageRepository>().GetRecentForUserAsync(input.UserId, Math.Clamp(input.Limit, 1, 50), token);
            IReadOnlyList<PageSummaryDto> list = [.. recent.Select(PageSummaryDto.From)];
            return list;
        }, cancellationToken: ct);
        return Ok(pages);
    }
}

// ---------- Public link: no sign-in, nothing but the page ----------

public sealed record GetPublicPageInput(string Token);

/// <summary>The page behind a public link. Anyone with the link reads it; no user is involved.</summary>
public sealed class GetPublicPageQuery(GetPublicPageInput input) : QueryBase<GetPublicPageInput, PublicPageDto>(input);

public sealed class GetPublicPageQueryHandler(IUnitOfWorkFactory factory) : QueryHandlerBase<GetPublicPageQuery, PublicPageDto>
{
    protected override async Task<Result<PublicPageDto>> HandleAsync(GetPublicPageQuery request, CancellationToken ct)
    {
        var token = request.Input.Token;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
            return Fail(PageErrors.NotFound);
        var page = await factory.ExecuteAsync(NotesModule.DatabaseKey, (ctx, t) =>
            ctx.AcquireRepository<IPageRepository>().FindByPublicTokenAsync(token, t), cancellationToken: ct);
        return page is null ? Fail(PageErrors.NotFound) : Ok(PublicPageDto.From(page));
    }
}

public sealed record GetPublicMediaInput(string Token, Guid MediaId, string? Purpose = null);

/// <summary>
/// An image or file a public page shows: served only when the page itself uses it (its blocks or its
/// cover), so a public link opens nothing else of the library.
/// </summary>
public sealed class GetPublicMediaQuery(GetPublicMediaInput input) : QueryBase<GetPublicMediaInput, MediaContentDto>(input);

public sealed class GetPublicMediaQueryHandler(
    IUnitOfWorkFactory factory,
    [FromKeyedServices(NotesModule.DatabaseKey)] IFileStorage fileStorage)
    : QueryHandlerBase<GetPublicMediaQuery, MediaContentDto>
{
    protected override async Task<Result<MediaContentDto>> HandleAsync(GetPublicMediaQuery request, CancellationToken ct)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.Token) || input.Token.Length > 64)
            return Fail(MediaErrors.NotFound);

        var found = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var page = await ctx.AcquireRepository<IPageRepository>().FindByPublicTokenAsync(input.Token, token);
            if (page is null)
                return null;
            var uses = await ctx.AcquireRepository<IPageMediaRepository>().GetByPageAsync(page.Id, token);
            if (uses.All(use => use.MediaId != input.MediaId))
                return null;
            // A deleted item still serves its bytes, as on the owner's side: the page still shows it.
            var item = await ctx.AcquireRepository<IMediaItemRepository>().GetByIdAsync(input.MediaId, token);
            if (item is null || item.UserId != page.UserId)
                return null;
            if (input.Purpose is null)
                return (Name: item.OriginalFileName, item.ContentType, item.StorageKey);
            var rendition = await ctx.AcquireRepository<IMediaRenditionRepository>().FindAsync(item.Id, input.Purpose, token);
            return rendition is null ? null : ((string Name, string ContentType, string StorageKey)?)(item.OriginalFileName, rendition.ContentType, rendition.StorageKey);
        }, cancellationToken: ct);

        if (found is null)
            return Fail(MediaErrors.NotFound);
        var blob = await fileStorage.GetAsync(found.Value.StorageKey, ct);
        return blob is null ? Fail(MediaErrors.NotFound) : Ok(new MediaContentDto(found.Value.Name, found.Value.ContentType, blob.Content));
    }
}
