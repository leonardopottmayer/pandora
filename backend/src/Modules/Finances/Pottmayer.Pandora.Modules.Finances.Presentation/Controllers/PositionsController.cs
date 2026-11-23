using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.AddPositionMovement;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreatePosition;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.ReportPositionValues;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UndoPositionMovement;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPosition;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPositions;
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
[Route("api/v{version:apiVersion}/finances/positions")]
public sealed class PositionsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? accountId = null, [FromQuery] Guid? classId = null, [FromQuery] bool includeClosed = false,
        CancellationToken ct = default)
        => (await sender.Send(new GetPositionsQuery(new GetPositionsInput(UserId, accountId, classId, includeClosed)), ct))
            .ToActionResult(errorMapper);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new GetPositionQuery(new GetPositionInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreatePositionRequest r, CancellationToken ct)
        => (await sender.Send(new CreatePositionCommand(new CreatePositionInput(
            UserId, r.AccountId, r.AssetId, r.Kind, r.OccurredOn, r.Quantity, r.UnitPrice, r.AverageCost, r.InvestedAmount,
            r.CurrentValue, r.Costs, r.Index, r.IndexPercent, r.SpreadRate, r.MaturityOn, r.Notes)), ct))
            .ToActionResult(errorMapper);

    [HttpPost("{id:guid}/movements")]
    public async Task<IActionResult> AddMovementAsync(Guid id, AddPositionMovementRequest r, CancellationToken ct)
        => (await sender.Send(new AddPositionMovementCommand(new AddPositionMovementInput(
            UserId, id, r.Kind, r.OccurredOn, r.Quantity, r.UnitPrice, r.GrossAmount, r.Costs, r.TaxWithheld,
            r.IncomeKind, r.SplitFactor, r.Notes)), ct))
            .ToActionResult(errorMapper);

    /// <summary>Undoes the latest movement; <c>false</c> means it was the only one and the position is gone.</summary>
    [HttpDelete("{id:guid}/movements/latest")]
    public async Task<IActionResult> UndoMovementAsync(Guid id, CancellationToken ct)
        => (await sender.Send(new UndoPositionMovementCommand(new UndoPositionMovementInput(UserId, id)), ct)).ToActionResult(errorMapper);

    [HttpPost("{id:guid}/valuations")]
    public async Task<IActionResult> ReportValueAsync(Guid id, ReportPositionValueRequest r, CancellationToken ct)
        => (await sender.Send(new ReportPositionValuesCommand(new ReportPositionValuesInput(
            UserId, r.ValuedOn, [new PositionValueItem(id, r.Value)])), ct))
            .ToActionResult(errorMapper);

    [HttpPost("valuations")]
    public async Task<IActionResult> ReportValuesAsync(ReportPositionValuesRequest r, CancellationToken ct)
        => (await sender.Send(new ReportPositionValuesCommand(new ReportPositionValuesInput(
            UserId, r.ValuedOn, [.. r.Items.Select(i => new PositionValueItem(i.PositionId, i.Value))])), ct))
            .ToActionResult(errorMapper);
}
