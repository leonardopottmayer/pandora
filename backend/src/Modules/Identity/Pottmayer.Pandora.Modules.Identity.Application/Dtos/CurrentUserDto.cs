namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

/// <summary>
/// The signed-in user, with the feature flags that are on and the permissions they hold (wildcards
/// expanded), so the client can hide what would be refused. The backend still checks every request.
/// </summary>
public sealed record CurrentUserDto(
    string Id,
    string Name,
    string Email,
    string Username,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Permissions);
