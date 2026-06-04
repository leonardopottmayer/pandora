namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record SignUpRequest(string Name, string Username, string Email, string Password);
