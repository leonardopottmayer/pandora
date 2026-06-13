using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CloseStatement;

public sealed record CloseStatementInput(Guid UserId, Guid StatementId);

public sealed class CloseStatementCommand(CloseStatementInput input)
    : CommandBase<CloseStatementInput, CardStatementDto>(input);
