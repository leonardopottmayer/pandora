using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.DeletePage;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.MovePage;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageArchived;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SetPageFavorite;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetBacklinks;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPage;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageTree;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/pages")]
public sealed class PagesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetPageTreeQuery(new GetPageTreeInput(UserId, includeArchived)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchAsync([FromQuery] string? q, CancellationToken ct)
    {
        var result = await sender.Send(new SearchPagesQuery(new SearchPagesInput(UserId, q)), ct);
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

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreatePageRequest request, CancellationToken ct)
    {
        var command = new CreatePageCommand(new CreatePageInput(
            UserId, request.Title, request.ParentId, request.Icon, request.ContentMarkdown));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdatePageRequest request, CancellationToken ct)
    {
        var command = new UpdatePageCommand(new UpdatePageInput(
            UserId, id, request.Title, request.Icon, request.ContentMarkdown));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MoveAsync(Guid id, MovePageRequest request, CancellationToken ct)
    {
        var command = new MovePageCommand(new MovePageInput(UserId, id, request.ParentId, request.OrderIndex));
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
}
