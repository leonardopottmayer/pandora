using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CloseStatement;

public sealed record CloseStatementInput(Guid UserId, Guid StatementId);

/// <summary>Manually closes a statement to new purchases ahead of its scheduled closing date.</summary>
[RequiresPermission(FinancesPermissions.Statements.Close)]
public sealed class CloseStatementCommand(CloseStatementInput input)
    : CommandBase<CloseStatementInput, CardStatementDto>(input);
