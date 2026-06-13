using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PayStatement;

public sealed record PayStatementInput(
    Guid UserId,
    Guid StatementId,
    Guid AccountId,
    decimal Amount,
    DateOnly? OccurredOn,
    string? Description,
    string? Notes,
    decimal? FxRate);

public sealed class PayStatementCommand(PayStatementInput input)
    : CommandBase<PayStatementInput, CardStatementDto>(input);
