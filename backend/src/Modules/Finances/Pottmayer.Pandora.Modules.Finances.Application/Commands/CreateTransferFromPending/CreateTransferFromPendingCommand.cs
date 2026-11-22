using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransferFromPending;

public sealed record CreateTransferFromPendingInput(
    Guid UserId,
    Guid OutflowPendingId,
    Guid InflowPendingId,
    string? Description,
    DateOnly? OccurredOn);

/// <summary>
/// Resolves two pending suggestions that are really the same transfer's two legs into an actual
/// transfer, approving both suggestions in the process.
/// </summary>
[RequiresPermission(FinancesPermissions.Pending.Approve)]
public sealed class CreateTransferFromPendingCommand(CreateTransferFromPendingInput input)
    : CommandBase<CreateTransferFromPendingInput, IReadOnlyList<TransactionDto>>(input);
