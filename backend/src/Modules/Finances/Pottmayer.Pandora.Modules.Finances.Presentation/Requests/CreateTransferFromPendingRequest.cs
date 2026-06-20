namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateTransferFromPendingRequest(
    Guid OutflowPendingId,
    Guid InflowPendingId,
    string? Description,
    DateOnly? OccurredOn);
