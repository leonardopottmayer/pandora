namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record RegenerateRecoveryCodesRequest(string Password, string Code);
