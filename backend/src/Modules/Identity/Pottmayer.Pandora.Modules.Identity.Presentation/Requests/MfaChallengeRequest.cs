namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record MfaChallengeRequest(string Ticket, string Code);
