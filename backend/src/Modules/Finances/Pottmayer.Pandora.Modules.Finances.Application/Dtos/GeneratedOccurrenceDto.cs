namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>
/// Result of a manual recurrence generation. <see cref="Destination"/> is either <c>"inbox"</c>
/// (a <see cref="PendingTransactionDto"/> is set) or <c>"transactions"</c> (a <see cref="TransactionDto"/> is set).
/// </summary>
public sealed record GeneratedOccurrenceDto(
    string Destination,
    PendingTransactionDto? Pending,
    TransactionDto? Transaction);
