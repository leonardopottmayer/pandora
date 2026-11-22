using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReopenStatement;

public sealed record ReopenStatementInput(Guid UserId, Guid StatementId);

/// <summary>Reopens a closed statement to new purchases. Fails if it's still open or already fully paid.</summary>
[RequiresPermission(FinancesPermissions.Statements.Close)]
public sealed class ReopenStatementCommand(ReopenStatementInput input)
    : CommandBase<ReopenStatementInput, CardStatementDto>(input);
