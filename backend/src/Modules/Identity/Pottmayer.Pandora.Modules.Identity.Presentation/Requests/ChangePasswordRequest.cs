namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
