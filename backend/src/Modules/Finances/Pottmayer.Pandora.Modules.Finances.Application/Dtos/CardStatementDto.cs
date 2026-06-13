using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record CardStatementDto(
    Guid Id,
    Guid CardId,
    string ReferenceMonth,
    DateOnly ClosingDate,
    DateOnly DueDate,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? OverdueAt)
{
    public static CardStatementDto From(CardStatement statement) =>
        new(
            statement.Id,
            statement.CardId,
            statement.ReferenceMonth,
            statement.ClosingDate,
            statement.DueDate,
            statement.Status.Value,
            statement.TotalAmount,
            statement.PaidAmount,
            statement.RemainingAmount,
            statement.ClosedAt,
            statement.PaidAt,
            statement.OverdueAt);
}
