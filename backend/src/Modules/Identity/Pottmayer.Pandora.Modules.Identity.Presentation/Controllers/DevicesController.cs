using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Devices.RegisterDevice;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Devices.RevokeDevice;
using Pottmayer.Pandora.Modules.Identity.Application.Queries.Devices.GetDevice;
using Pottmayer.Pandora.Modules.Identity.Application.Queries.Devices.GetDevices;
using Pottmayer.Pandora.Modules.Identity.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Identity.Presentation.Controllers;

/// <summary>
/// Paired devices. Pairing, listing and revoking are the user's (session); <c>me</c> is the device's own
/// view of itself, reachable only with a device key.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/identity/devices")]
public sealed class DevicesController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var result = await sender.Send(new GetDevicesQuery(new GetDevicesInput(userId)), ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Pairs a device. The response carries its key — the only time it is ever returned.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var command = new RegisterDeviceCommand(new RegisterDeviceInput(
            userId, request.Name, request.Platform, request.Form, request.Scopes));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid id, CancellationToken ct)
    {
        var userId = userContextAccessor.Context.User!.Id;
        var result = await sender.Send(new RevokeDeviceCommand(new RevokeDeviceInput(userId, id)), ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize(AuthenticationSchemes = DeviceAuthorization.Scheme, Policy = DeviceAuthorization.Policy)]
    [HttpGet("me")]
    public async Task<IActionResult> MeAsync(CancellationToken ct)
    {
        var deviceId = Guid.Parse(User.FindFirst(DeviceAuthorization.DeviceIdClaim)!.Value);
        var result = await sender.Send(new GetDeviceQuery(new GetDeviceInput(deviceId)), ct);
        return result.ToActionResult(errorMapper);
    }
}
