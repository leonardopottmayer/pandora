using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAccount;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteAccount;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.SetAccountArchived;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateAccount;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccount;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccountBalance;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;
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
[Route("api/v{version:apiVersion}/finances/accounts")]
public sealed class AccountsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetAccountsQuery(new GetAccountsInput(UserId, includeArchived)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetAccountQuery(new GetAccountInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateAccountRequest request, CancellationToken ct)
    {
        var command = new CreateAccountCommand(new CreateAccountInput(
            UserId, request.Name, request.Type, request.Currency, request.Institution,
            request.Description, request.Color, request.Icon, request.DisplayOrder, request.OpeningBalance));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct)
    {
        var command = new UpdateAccountCommand(new UpdateAccountInput(
            UserId, id, request.Name, request.Type, request.Institution,
            request.Description, request.Color, request.Icon, request.DisplayOrder));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteAccountCommand(new DeleteAccountInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var command = new SetAccountArchivedCommand(new SetAccountArchivedInput(UserId, id, Archived: true));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/unarchive")]
    public async Task<IActionResult> UnarchiveAsync(Guid id, CancellationToken ct)
    {
        var command = new SetAccountArchivedCommand(new SetAccountArchivedInput(UserId, id, Archived: false));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> GetBalanceAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetAccountBalanceQuery(new GetAccountBalanceInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetTransactionsAsync(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? kind,
        [FromQuery] string? status,
        [FromQuery] string? text,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var query = new GetTransactionsQuery(new GetTransactionsInput(
            UserId, id, from, to, kind, status, null, null, text, null, skip, take));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }
}
