using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Users.Application.Commands.UpsertPreferences;
using Pottmayer.Pandora.Modules.Users.Application.Queries.GetPreferences;
using Pottmayer.Pandora.Modules.Users.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Users.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users/preferences")]
public sealed class PreferencesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var query = new GetPreferencesQuery(new GetPreferencesInput(userId));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut]
    public async Task<IActionResult> UpsertAsync(UpsertPreferencesRequest request, CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var command = new UpsertPreferencesCommand(new UpsertPreferencesInput(userId, request.Theme));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
