using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.RefreshToken;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignOut;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignUp;
using Pottmayer.Pandora.Modules.Identity.Presentation.Requests;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Identity.Presentation.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/identity/auth")]
public sealed class AuthController(ISender sender, IHttpErrorMapper errorMapper) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<IActionResult> SignUpAsync(SignUpRequest request, CancellationToken ct)
    {
        var command = new SignUpCommand(
            new SignUpInput(request.Name, request.Username, request.Email, request.Password));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [AllowAnonymous]
    [HttpPost("signin")]
    public async Task<IActionResult> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        var command = new SignInCommand(new SignInInput(request.EmailOrUsername, request.Password));
        var result  = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [AllowAnonymous]
    [HttpPost("activate")]
    public async Task<IActionResult> ActivateAsync(ActivateRequest request, CancellationToken ct)
    {
        var command = new ActivateAccountCommand(new ActivateAccountInput(request.Token));
        var result  = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(new RefreshTokenInput(request.RefreshToken));
        var result  = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize]
    [HttpPost("signout")]
    public async Task<IActionResult> SignOutAsync(RefreshRequest request, CancellationToken ct)
    {
        var command = new SignOutCommand(new SignOutInput(request.RefreshToken));
        var result  = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
