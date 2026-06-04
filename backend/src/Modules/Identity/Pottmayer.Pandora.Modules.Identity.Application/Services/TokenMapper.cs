using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;

namespace Pottmayer.Pandora.Modules.Identity.Application.Services;

public static class TokenMapper
{
    public static AuthenticationResult ToAuthResult(Guid userId) => new()
    {
        Subject = userId.ToString(),
        Claims =
        [
            new ClaimData(nameof(UserData.Id), userId.ToString())
        ]
    };
}
