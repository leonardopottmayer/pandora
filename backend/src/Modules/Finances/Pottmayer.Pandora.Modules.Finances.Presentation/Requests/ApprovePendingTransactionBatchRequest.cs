namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record ApprovePendingTransactionBatchRequest(IReadOnlyList<Guid> Ids);
