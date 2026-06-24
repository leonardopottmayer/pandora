using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CloseStatement;

public sealed record CloseStatementInput(Guid UserId, Guid StatementId);

/// <summary>Manually closes a statement to new purchases ahead of its scheduled closing date.</summary>
public sealed class CloseStatementCommand(CloseStatementInput input)
    : CommandBase<CloseStatementInput, CardStatementDto>(input);
