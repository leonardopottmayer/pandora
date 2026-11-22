using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SavePreferences;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetPreferences;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Agenda.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agenda/preferences")]
public sealed class AgendaPreferencesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The user's Agenda preferences; the defaults when never saved.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetPreferencesQuery(new GetPreferencesInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut]
    public async Task<IActionResult> SaveAsync([FromBody] SaveAgendaPreferencesRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SavePreferencesCommand(new SavePreferencesInput(UserId, body.DefaultAlertOffsetMinutes)), ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    public sealed record SaveAgendaPreferencesRequest(int DefaultAlertOffsetMinutes);
}
