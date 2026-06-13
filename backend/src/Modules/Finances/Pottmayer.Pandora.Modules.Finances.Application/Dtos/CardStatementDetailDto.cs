namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record CardStatementDetailDto(
    CardStatementDto Statement,
    IReadOnlyList<TransactionDto> Transactions);
