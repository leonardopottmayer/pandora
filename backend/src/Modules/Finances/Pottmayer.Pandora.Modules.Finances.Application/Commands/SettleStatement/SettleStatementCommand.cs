using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SettleStatement;

public sealed record SettleStatementInput(
    Guid UserId,
    Guid StatementId,
    DateOnly? OccurredOn,
    string? Notes);

/// <summary>
/// Settles a statement's whole outstanding balance without cash: records a <c>statement-writeoff</c>
/// transaction that clears the balance but debits no account. Used at onboarding for pre-Pandora
/// statements so they don't sit open/overdue nor pull fake money out of a checking account.
/// </summary>
public sealed class SettleStatementCommand(SettleStatementInput input)
    : CommandBase<SettleStatementInput, CardStatementDto>(input);
