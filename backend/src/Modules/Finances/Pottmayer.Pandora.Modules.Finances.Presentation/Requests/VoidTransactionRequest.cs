namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record VoidTransactionRequest(string? Reason, bool VoidEntirePlan = false);
