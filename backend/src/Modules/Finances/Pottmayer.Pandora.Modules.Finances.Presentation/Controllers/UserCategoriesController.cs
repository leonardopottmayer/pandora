using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateUserCategory;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.SetUserCategoryActive;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateUserCategory;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetUserCategories;
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
[Route("api/v{version:apiVersion}/finances/categories")]
public sealed class UserCategoriesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    private Guid UserId => userContextAccessor.Context.User!.Id;

    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var query = new GetUserCategoriesQuery(new GetUserCategoriesInput(UserId, includeInactive));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateUserCategoryRequest request, CancellationToken ct)
    {
        var command = new CreateUserCategoryCommand(new CreateUserCategoryInput(
            UserId, request.Name, request.Nature, request.ParentCategoryId,
            request.Color, request.Icon, request.DisplayOrder));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateUserCategoryRequest request, CancellationToken ct)
    {
        var command = new UpdateUserCategoryCommand(new UpdateUserCategoryInput(
            UserId, id, request.Name, request.Color, request.Icon, request.DisplayOrder));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> ActivateAsync(Guid id, CancellationToken ct)
    {
        var command = new SetUserCategoryActiveCommand(new SetUserCategoryActiveInput(UserId, id, Active: true));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateAsync(Guid id, CancellationToken ct)
    {
        var command = new SetUserCategoryActiveCommand(new SetUserCategoryActiveInput(UserId, id, Active: false));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
