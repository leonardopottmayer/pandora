namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record SettleStatementRequest(
    DateOnly? OccurredOn,
    string? Notes);
