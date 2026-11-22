using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.SaveNotesPreferences;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetNotesPreferences;
using Pottmayer.Pandora.Modules.Notes.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Notes.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notes/preferences")]
public sealed class NotesPreferencesController(ISender sender, IHttpErrorMapper errorMapper, IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct) =>
        (await sender.Send(new GetNotesPreferencesQuery(new GetNotesPreferencesInput(UserId)), ct)).ToActionResult(errorMapper);

    [HttpPut]
    public async Task<IActionResult> SaveAsync(SaveNotesPreferencesRequest request, CancellationToken ct) =>
        (await sender.Send(new SaveNotesPreferencesCommand(new SaveNotesPreferencesInput(UserId, request.DailyTemplateId)), ct)).ToActionResult(errorMapper);
}
