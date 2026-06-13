using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

public sealed record StatementResolution(string ReferenceMonth, DateOnly ClosingDate, DateOnly DueDate);

public interface IStatementResolver
{
    StatementResolution Resolve(Card card, DateOnly purchaseDate);
}
