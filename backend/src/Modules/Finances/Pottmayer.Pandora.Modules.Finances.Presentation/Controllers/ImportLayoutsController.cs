using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetImportLayouts;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Finances.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/finances/import-layouts")]
public sealed class ImportLayoutsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetImportLayoutsQuery(new GetImportLayoutsInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }
}
