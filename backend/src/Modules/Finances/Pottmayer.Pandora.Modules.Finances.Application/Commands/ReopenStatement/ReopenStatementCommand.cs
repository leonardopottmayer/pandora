using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReopenStatement;

public sealed record ReopenStatementInput(Guid UserId, Guid StatementId);

public sealed class ReopenStatementCommand(ReopenStatementInput input)
    : CommandBase<ReopenStatementInput, CardStatementDto>(input);
