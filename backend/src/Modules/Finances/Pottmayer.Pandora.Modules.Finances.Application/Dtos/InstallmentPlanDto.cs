namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>One installment of a plan with the state of its statement (criterion: the plan records the state of each installment).</summary>
public sealed record InstallmentItemDto(
    short Number,
    Guid TransactionId,
    Guid StatementId,
    string ReferenceMonth,
    decimal Amount,
    string Status,
    string StatementStatus);

public sealed record InstallmentPlanDto(
    Guid Id,
    Guid CardId,
    string Origin,
    string Description,
    int InstallmentCount,
    decimal TotalAmount,
    bool TotalIsEstimate,
    string FirstReferenceMonth,
    decimal RemainingAmount,
    int PaidInstallments,
    IReadOnlyList<InstallmentItemDto> Installments);
