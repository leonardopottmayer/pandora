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

/// <summary>
/// Pays down a statement from an account: creates the payment transaction and applies it to the
/// statement's balance. Cross-currency payments require an explicit FX rate.
/// </summary>
public sealed class PayStatementCommand(PayStatementInput input)
    : CommandBase<PayStatementInput, CardStatementDto>(input);
