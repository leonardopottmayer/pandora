using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransfer;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.PostTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.VoidTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransactions;
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
[Route("api/v{version:apiVersion}/finances/transactions")]
public sealed class TransactionsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? accountId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? kind,
        [FromQuery] string? status,
        [FromQuery] Guid? systemCategoryId,
        [FromQuery] Guid? userCategoryId,
        [FromQuery] string? text,
        [FromQuery] string? origin,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var query = new GetTransactionsQuery(new GetTransactionsInput(
            UserId, accountId, from, to, kind, status, systemCategoryId, userCategoryId, text, origin, skip, take));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateTransactionRequest request, CancellationToken ct)
    {
        var command = new CreateTransactionCommand(new CreateTransactionInput(
            UserId, request.AccountId, request.CardId, request.CardStatementId, request.Kind, request.Amount, request.OccurredOn, request.Description,
            request.Payee, request.Notes, request.SystemCategoryId, request.UserCategoryId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> TransferAsync(CreateTransferRequest request, CancellationToken ct)
    {
        var command = new CreateTransferCommand(new CreateTransferInput(
            UserId, request.FromAccountId, request.ToAccountId, request.AmountOut, request.AmountIn,
            request.FxRate, request.OccurredOn, request.Description, request.Notes));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken ct)
    {
        var command = new UpdateTransactionCommand(new UpdateTransactionInput(
            UserId, id, request.Description, request.Payee, request.Notes,
            request.SystemCategoryId, request.UserCategoryId));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/post")]
    public async Task<IActionResult> PostAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new PostTransactionCommand(new PostTransactionInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> VoidAsync(Guid id, VoidTransactionRequest? request, CancellationToken ct)
    {
        var command = new VoidTransactionCommand(new VoidTransactionInput(UserId, id, request?.Reason));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
