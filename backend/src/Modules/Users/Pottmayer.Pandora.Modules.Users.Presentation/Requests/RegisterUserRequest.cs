namespace Pottmayer.Pandora.Modules.Users.Presentation.Requests;

public sealed record RegisterUserRequest(string Name, string Username, string Email, string Password);
