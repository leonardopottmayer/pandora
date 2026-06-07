namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record DisableMfaRequest(string Password, string Code);
