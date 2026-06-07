using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.Activation;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.ChangePassword;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.PasswordReset;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.RefreshToken;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignIn;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignOut;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.SignUp;
using Pottmayer.Pandora.Modules.Identity.Presentation.Requests;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Identity.Presentation.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/identity/auth")]
public sealed class AuthController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
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
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var command = new RequestPasswordResetCommand(new RequestPasswordResetInput(request.Email));
        var result  = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [AllowAnonymous]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var command = new ResetPasswordCommand(new ResetPasswordInput(request.Token, request.NewPassword));
        var result  = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    [Authorize]
    [HttpPost("password/change")]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId  = userContextAccessor.Context.User!.Id;
        var command = new ChangePasswordCommand(
            new ChangePasswordInput(userId, request.CurrentPassword, request.NewPassword));
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
