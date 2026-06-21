using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateRecurringTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteRecurringTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.GenerateRecurringTransactionOccurrence;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.PauseRecurringTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.ResumeRecurringTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateRecurringTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransaction;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetRecurringTransactions;
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
[Route("api/v{version:apiVersion}/finances/recurring-transactions")]
public sealed class RecurringTransactionsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetRecurringTransactionsQuery(new GetRecurringTransactionsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetRecurringTransactionQuery(new GetRecurringTransactionInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateRecurringTransactionRequest request, CancellationToken ct)
    {
        var command = new CreateRecurringTransactionCommand(new CreateRecurringTransactionInput(
            UserId,
            request.Name,
            request.AccountId,
            request.CardId,
            request.Kind,
            request.Amount,
            request.AmountIsEstimate,
            request.Description,
            request.Payee,
            request.SystemCategoryId,
            request.UserCategoryId,
            request.Frequency,
            request.Interval,
            request.DayOfMonth,
            request.Weekday,
            request.StartDate,
            request.EndDate,
            request.MaxOccurrences,
            request.AutoPost,
            request.AutoGenerate));

        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateRecurringTransactionRequest request, CancellationToken ct)
    {
        var command = new UpdateRecurringTransactionCommand(new UpdateRecurringTransactionInput(
            UserId, id,
            request.Name,
            request.Amount,
            request.AmountIsEstimate,
            request.Description,
            request.Payee,
            request.SystemCategoryId,
            request.UserCategoryId,
            request.EndDate,
            request.MaxOccurrences,
            request.AutoPost,
            request.AutoGenerate));

        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new DeleteRecurringTransactionCommand(new DeleteRecurringTransactionInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> PauseAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new PauseRecurringTransactionCommand(new PauseRecurringTransactionInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> ResumeAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new ResumeRecurringTransactionCommand(new ResumeRecurringTransactionInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> GenerateAsync(Guid id, GenerateRecurringTransactionOccurrenceRequest request, CancellationToken ct)
    {
        var command = new GenerateRecurringTransactionOccurrenceCommand(new GenerateRecurringTransactionOccurrenceInput(
            UserId, id,
            request.Destination,
            request.AdvanceSchedule,
            request.OccurredOn,
            request.Amount,
            request.Description,
            request.Payee,
            request.Notes,
            request.SystemCategoryId,
            request.UserCategoryId));

        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
