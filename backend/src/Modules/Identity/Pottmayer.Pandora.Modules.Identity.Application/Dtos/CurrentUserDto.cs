namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

public sealed record CurrentUserDto(string Id, string Name, string Email, string Username);
