using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTag;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteTag;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkTag;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UnlinkTag;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateTag;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTagLinks;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTags;
using Pottmayer.Pandora.Modules.Finances.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Finances.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/finances/tags")]
public sealed class TagsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTagsQuery(new GetTagsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateTagRequest request, CancellationToken ct)
    {
        var command = new CreateTagCommand(new CreateTagInput(UserId, request.Name, request.Color));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct)
    {
        var command = new UpdateTagCommand(new UpdateTagInput(UserId, id, request.Name, request.Color));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteTagCommand(new DeleteTagInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/links")]
    public async Task<IActionResult> GetLinksAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetTagLinksQuery(new GetTagLinksInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/links")]
    public async Task<IActionResult> LinkAsync(Guid id, LinkTagRequest request, CancellationToken ct)
    {
        var command = new LinkTagCommand(new LinkTagInput(UserId, id, request.EntityType, request.EntityId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}/links/{entityType}/{entityId:guid}")]
    public async Task<IActionResult> UnlinkAsync(Guid id, string entityType, Guid entityId, CancellationToken ct)
    {
        var command = new UnlinkTagCommand(new UnlinkTagInput(UserId, id, entityType, entityId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
