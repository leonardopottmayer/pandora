using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetSystemCategories;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Finances.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/finances/categories/system")]
public sealed class SystemCategoriesController(ISender sender, IHttpErrorMapper errorMapper) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? nature,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var query = new GetSystemCategoriesQuery(new GetSystemCategoriesInput(nature, includeInactive));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }
}
