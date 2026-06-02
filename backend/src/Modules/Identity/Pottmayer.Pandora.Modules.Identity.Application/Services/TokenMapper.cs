using Pottmayer.Pandora.Modules.Users.Contracts.Authentication;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;

namespace Pottmayer.Pandora.Modules.Identity.Application.Services;

public static class TokenMapper
{
    public static AuthenticationResult ToAuthResult(UserAuthDto user) => new()
    {
        Subject = user.Id.ToString(),
        Claims =
        [
            new ClaimData("Id", user.Id.ToString())
        ]
    };
}
