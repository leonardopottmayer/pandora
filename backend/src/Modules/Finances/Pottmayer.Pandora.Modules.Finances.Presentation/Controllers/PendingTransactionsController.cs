using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransactionBatch;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransferFromPending;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkPendingTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RejectPendingTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdatePendingTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPendingTransactions;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
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
[Route("api/v{version:apiVersion}/finances/pending-transactions")]
public sealed class PendingTransactionsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? source,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? cardId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var filter = new PendingTransactionFilter(source, accountId, cardId, from, to, skip, take);
        var result = await sender.Send(new GetPendingTransactionsQuery(new GetPendingTransactionsInput(UserId, filter)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdatePendingTransactionRequest request, CancellationToken ct)
    {
        var command = new UpdatePendingTransactionCommand(new UpdatePendingTransactionInput(
            UserId, id,
            request.Kind,
            request.Amount,
            request.OccurredOn,
            request.Description,
            request.Payee,
            request.Notes,
            request.SystemCategoryId,
            request.UserCategoryId,
            request.SuggestedStatementId));

        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new ApprovePendingTransactionCommand(new ApprovePendingTransactionInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectAsync(Guid id, RejectPendingTransactionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new RejectPendingTransactionCommand(new RejectPendingTransactionInput(UserId, id, request.Reason)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("approve-batch")]
    public async Task<IActionResult> ApproveBatchAsync(ApprovePendingTransactionBatchRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new ApprovePendingTransactionBatchCommand(new ApprovePendingTransactionBatchInput(UserId, request.Ids)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/link")]
    public async Task<IActionResult> LinkAsync(Guid id, LinkPendingTransactionRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new LinkPendingTransactionCommand(new LinkPendingTransactionInput(UserId, id, request.TransactionId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> TransferAsync(CreateTransferFromPendingRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new CreateTransferFromPendingCommand(new CreateTransferFromPendingInput(
            UserId, request.OutflowPendingId, request.InflowPendingId, request.Description, request.OccurredOn)), ct);
        return result.ToActionResult(errorMapper);
    }
}
