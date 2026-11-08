using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.OpenDailyNote;
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
[Route("api/v{version:apiVersion}/notes/daily")]
public sealed class DailyNotesController(ISender sender, IHttpErrorMapper errorMapper, IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> OpenAsync(OpenDailyNoteRequest request, CancellationToken ct) =>
        (await sender.Send(new OpenDailyNoteCommand(new OpenDailyNoteInput(
            userContextAccessor.Context.User!.Id, request.Date, CultureInfo.CurrentUICulture.Name)), ct)).ToActionResult(errorMapper);
}
