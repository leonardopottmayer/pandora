namespace Pottmayer.Pandora.Modules.Users.Contracts.Authentication;

public sealed record UserAuthDto(Guid Id, string Username, string Email, string Name);
