using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransferFromPending;

public sealed record CreateTransferFromPendingInput(
    Guid UserId,
    Guid OutflowPendingId,
    Guid InflowPendingId,
    string? Description,
    DateOnly? OccurredOn);

public sealed class CreateTransferFromPendingCommand(CreateTransferFromPendingInput input)
    : CommandBase<CreateTransferFromPendingInput, IReadOnlyList<TransactionDto>>(input);
