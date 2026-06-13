using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateCard;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteCard;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.SetCardArchived;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateCard;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCard;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardAvailableLimit;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardStatements;
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
[Route("api/v{version:apiVersion}/finances/cards")]
public sealed class CardsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetCardsQuery(new GetCardsInput(UserId, includeArchived)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCardQuery(new GetCardInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateCardRequest request, CancellationToken ct)
    {
        var command = new CreateCardCommand(new CreateCardInput(
            UserId, request.Name, request.Brand, request.LastFour, request.CreditLimit,
            request.ClosingDay, request.DueDay, request.Currency, request.DefaultPaymentAccountId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateCardRequest request, CancellationToken ct)
    {
        var command = new UpdateCardCommand(new UpdateCardInput(
            UserId, id, request.Name, request.Brand, request.LastFour, request.CreditLimit,
            request.ClosingDay, request.DueDay, request.DefaultPaymentAccountId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteCardCommand(new DeleteCardInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SetCardArchivedCommand(new SetCardArchivedInput(UserId, id, true)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SetCardArchivedCommand(new SetCardArchivedInput(UserId, id, false)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/statements")]
    public async Task<IActionResult> GetStatementsAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCardStatementsQuery(new GetCardStatementsInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/available-limit")]
    public async Task<IActionResult> GetAvailableLimitAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCardAvailableLimitQuery(new GetCardAvailableLimitInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }
}
