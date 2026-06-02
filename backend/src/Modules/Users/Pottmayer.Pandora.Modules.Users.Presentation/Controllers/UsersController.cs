using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Users.Application.Commands.RegisterUser;
using Pottmayer.Pandora.Modules.Users.Presentation.Requests;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Users.Presentation.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users/users")]
public sealed class UsersController(ISender sender, IHttpErrorMapper errorMapper) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(RegisterUserRequest request, CancellationToken ct)
    {
        var command = new RegisterUserCommand(
            new RegisterUserInput(request.Name, request.Username, request.Email, request.Password));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }
}
