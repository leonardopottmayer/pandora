using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePageFromTemplate;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageTemplate;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.DeletePage;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.MovePage;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.PageSettings;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.RestorePageVersion;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageArchived;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageFavorite;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SummarizePage;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetBacklinks;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPage;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageGraph;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageTree;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersion;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersions;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.PageViews;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;
using Pottmayer.Pandora.Modules.Notes.Presentation.Filters;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

[ApiController]
[Authorize]
[TypeFilter(typeof(PageHistoryConflictFilter))]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/pages")]
public sealed class PagesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpPost("{id:guid}/ai/summary")]
    public async Task<IActionResult> SummaryAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new SummarizePageCommand(new SummarizePageInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> VersionsAsync(Guid id, [FromQuery] long? before, CancellationToken ct)
    {
        var result = await sender.Send(new GetPageVersionsQuery(new GetPageVersionsInput(UserId, id, before)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/versions/{number:long}")]
    public async Task<IActionResult> VersionAsync(Guid id, long number, CancellationToken ct)
    {
        var result = await sender.Send(new GetPageVersionQuery(new GetPageVersionInput(UserId, id, number)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/versions/{number:long}/restore")]
    public async Task<IActionResult> RestoreVersionAsync(Guid id, long number, RestorePageVersionRequest request, CancellationToken ct)
    {
        var command = new RestorePageVersionCommand(new RestorePageVersionInput(UserId, id, number, request.ExpectedVersion));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] bool includeArchived = false,
        [FromQuery] Guid[]? tagIds = null,
        [FromQuery] Guid? spaceId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetPageTreeQuery(new GetPageTreeInput(UserId, includeArchived, tagIds, spaceId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string? q,
        [FromQuery] Guid[]? tagIds = null,
        [FromQuery] Guid? spaceId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new SearchPagesQuery(new SearchPagesInput(UserId, q, tagIds, spaceId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("graph")]
    public async Task<IActionResult> GraphAsync(
        [FromQuery] Guid[]? tagIds = null,
        [FromQuery] Guid? spaceId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetPageGraphQuery(new GetPageGraphInput(UserId, RootPageId: null, Depth: 1, tagIds, spaceId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetPageQuery(new GetPageInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/backlinks")]
    public async Task<IActionResult> BacklinksAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetBacklinksQuery(new GetBacklinksInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/graph")]
    public async Task<IActionResult> LocalGraphAsync(
        Guid id,
        [FromQuery] int depth = 1,
        [FromQuery] Guid[]? tagIds = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetPageGraphQuery(new GetPageGraphInput(UserId, id, depth, tagIds)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreatePageRequest request, CancellationToken ct)
    {
        var command = new CreatePageCommand(new CreatePageInput(
            UserId, request.Title, request.ParentId, request.Icon, request.Content?.GetRawText(), request.SpaceId, FolderId: request.FolderId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdatePageRequest request, CancellationToken ct)
    {
        var command = new UpdatePageCommand(new UpdatePageInput(
            UserId, id, request.Title, request.Icon, request.Content?.GetRawText(), request.Source));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}/template")]
    public async Task<IActionResult> TemplateAsync(Guid id, SetPageTemplateRequest request, CancellationToken ct)
        => (await sender.Send(new SetPageTemplateCommand(new SetPageTemplateInput(UserId, id, request.IsTemplate)), ct)).ToActionResult(errorMapper);

    [HttpPost("from-template")]
    public async Task<IActionResult> FromTemplateAsync(CreatePageFromTemplateRequest request, CancellationToken ct)
        => (await sender.Send(new CreatePageFromTemplateCommand(new CreatePageFromTemplateInput(
            UserId, request.TemplateId, request.Title, request.SpaceId, request.FolderId)), ct)).ToActionResult(errorMapper);

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MoveAsync(Guid id, MovePageRequest request, CancellationToken ct)
    {
        var command = new MovePageCommand(new MovePageInput(UserId, id, request.ParentId, request.OrderIndex, request.SpaceId, request.FolderId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/favorite")]
    public async Task<IActionResult> FavoriteAsync(Guid id, CancellationToken ct)
    {
        var command = new SetPageFavoriteCommand(new SetPageFavoriteInput(UserId, id, Favorite: true));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/unfavorite")]
    public async Task<IActionResult> UnfavoriteAsync(Guid id, CancellationToken ct)
    {
        var command = new SetPageFavoriteCommand(new SetPageFavoriteInput(UserId, id, Favorite: false));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var command = new SetPageArchivedCommand(new SetPageArchivedInput(UserId, id, Archived: true));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var command = new SetPageArchivedCommand(new SetPageArchivedInput(UserId, id, Archived: false));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeletePageCommand(new DeletePageInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The pages opened last, newest first.</summary>
    [HttpGet("recent")]
    public async Task<IActionResult> RecentAsync([FromQuery] int limit = 20, CancellationToken ct = default)
        => (await sender.Send(new GetRecentPagesQuery(new GetRecentPagesInput(UserId, limit)), ct)).ToActionResult(errorMapper);

    /// <summary>Notes that the page was opened; not an edit.</summary>
    [HttpPost("{id:guid}/viewed")]
    public async Task<IActionResult> ViewedAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new MarkPageViewedCommand(new MarkPageViewedInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpPut("{id:guid}/appearance")]
    public async Task<IActionResult> AppearanceAsync(Guid id, SetPageAppearanceRequest request, CancellationToken ct)
        => (await sender.Send(new SetPageAppearanceCommand(new SetPageAppearanceInput(
            UserId, id, request.Cover, request.Width, request.Font, request.SmallText)), ct)).ToActionResult(errorMapper);

    [HttpPut("{id:guid}/lock")]
    public async Task<IActionResult> LockAsync(Guid id, SetPageLockedRequest request, CancellationToken ct)
        => (await sender.Send(new SetPageLockedCommand(new SetPageLockedInput(UserId, id, request.Locked)), ct)).ToActionResult(errorMapper);

    [HttpPut("{id:guid}/verification")]
    public async Task<IActionResult> VerificationAsync(Guid id, SetPageVerificationRequest request, CancellationToken ct)
        => (await sender.Send(new SetPageVerificationCommand(new SetPageVerificationInput(UserId, id, request.Verified, request.Days)), ct))
            .ToActionResult(errorMapper);

    /// <summary>Opens or closes the page's read-only public link; the token comes back in <c>publicToken</c>.</summary>
    [HttpPut("{id:guid}/public")]
    public async Task<IActionResult> PublicAsync(Guid id, SetPagePublicRequest request, CancellationToken ct)
        => (await sender.Send(new SetPagePublicCommand(new SetPagePublicInput(UserId, id, request.Public)), ct)).ToActionResult(errorMapper);
}
