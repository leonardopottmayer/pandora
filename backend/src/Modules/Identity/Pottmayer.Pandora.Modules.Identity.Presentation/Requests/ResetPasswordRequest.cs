namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
