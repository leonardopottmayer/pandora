using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CloseStatement;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.PayStatement;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.SetEntityTags;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetStatement;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
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
[Route("api/v{version:apiVersion}/finances/statements")]
public sealed class StatementsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetStatementQuery(new GetStatementInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> PayAsync(Guid id, PayStatementRequest request, CancellationToken ct)
    {
        var command = new PayStatementCommand(new PayStatementInput(
            UserId, id, request.AccountId, request.Amount, request.OccurredOn, request.Description, request.Notes, request.FxRate));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> CloseAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new CloseStatementCommand(new CloseStatementInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}/tags")]
    public async Task<IActionResult> SetTagsAsync(Guid id, SetEntityTagsRequest request, CancellationToken ct)
    {
        var command = new SetEntityTagsCommand(new SetEntityTagsInput(
            UserId, TaggableEntityType.CardStatement.Value, id, request.TagIds));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
