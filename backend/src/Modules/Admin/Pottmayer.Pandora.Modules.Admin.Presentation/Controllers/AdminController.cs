using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.ChangeUserRoles;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.CreateRole;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.DeleteRole;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.RevokeUserSessions;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.SetFeature;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.SetUserDisabled;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.SetUserExceptions;
using Pottmayer.Pandora.Modules.Admin.Application.Commands.UpdateRole;
using Pottmayer.Pandora.Modules.Admin.Application.Queries.GetAudit;
using Pottmayer.Pandora.Modules.Admin.Application.Queries.GetCatalog;
using Pottmayer.Pandora.Modules.Admin.Application.Queries.GetFeatures;
using Pottmayer.Pandora.Modules.Admin.Application.Queries.GetRoles;
using Pottmayer.Pandora.Modules.Admin.Application.Queries.GetUser;
using Pottmayer.Pandora.Modules.Admin.Application.Queries.GetUsers;
using Pottmayer.Pandora.Modules.Admin.Presentation.Filters;
using Pottmayer.Pandora.Modules.Admin.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Admin.Presentation.Controllers;

/// <summary>
/// The admin panel. Every request carries its own <c>admin.*</c> permission (checked by the mediator),
/// and the caller's account needs two-step verification.
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[TypeFilter(typeof(RequireMfaFilter))]
public sealed class AdminController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalogAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetCatalogQuery(new GetCatalogInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("features")]
    public async Task<IActionResult> GetFeaturesAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetFeaturesQuery(new GetFeaturesInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("features/{key}")]
    public async Task<IActionResult> SetFeatureAsync(string key, [FromBody] SetFeatureRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SetFeatureCommand(new SetFeatureInput(UserId, key, body.Enabled)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRolesAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetRolesQuery(new GetRolesInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRoleAsync([FromBody] RoleRequest body, CancellationToken ct)
    {
        var command = new CreateRoleCommand(new CreateRoleInput(UserId, body.Name, body.Description, body.Patterns ?? []));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("roles/{id:guid}")]
    public async Task<IActionResult> UpdateRoleAsync(Guid id, [FromBody] RoleRequest body, CancellationToken ct)
    {
        var command = new UpdateRoleCommand(new UpdateRoleInput(UserId, id, body.Name, body.Description, body.Patterns ?? []));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpDelete("roles/{id:guid}")]
    public async Task<IActionResult> DeleteRoleAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteRoleCommand(new DeleteRoleInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsersAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetUsersQuery(new GetUsersInput(UserId)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUserAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserQuery(new GetUserInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Gives a role to, or takes it from, several users at once.</summary>
    [HttpPost("users/roles")]
    public async Task<IActionResult> ChangeUserRolesAsync([FromBody] ChangeUserRolesRequest body, CancellationToken ct)
    {
        var command = new ChangeUserRolesCommand(new ChangeUserRolesInput(UserId, body.UserIds, body.RoleId, body.Assign));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("users/{id:guid}/exceptions")]
    public async Task<IActionResult> SetUserExceptionsAsync(Guid id, [FromBody] SetUserExceptionsRequest body, CancellationToken ct)
    {
        var command = new SetUserExceptionsCommand(new SetUserExceptionsInput(UserId, id, body.Exceptions ?? []));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPut("users/{id:guid}/disabled")]
    public async Task<IActionResult> SetUserDisabledAsync(Guid id, [FromBody] SetUserDisabledRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new SetUserDisabledCommand(new SetUserDisabledInput(UserId, id, body.Disabled)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpPost("users/{id:guid}/revoke-sessions")]
    public async Task<IActionResult> RevokeUserSessionsAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RevokeUserSessionsCommand(new RevokeUserSessionsInput(UserId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditAsync(
        [FromQuery] Guid? actorId,
        [FromQuery] string? targetType,
        [FromQuery] string? targetId,
        [FromQuery] DateTimeOffset? before,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var query = new GetAuditQuery(new GetAuditInput(UserId, actorId, targetType, targetId, before, limit));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;
}
