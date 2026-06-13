namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record PayStatementRequest(
    Guid AccountId,
    decimal Amount,
    DateOnly? OccurredOn,
    string? Description,
    string? Notes,
    decimal? FxRate);
