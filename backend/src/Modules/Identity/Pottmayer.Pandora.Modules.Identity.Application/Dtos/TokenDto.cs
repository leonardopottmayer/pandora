namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

public sealed record TokenDto(
    string AccessToken,
    long   AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
