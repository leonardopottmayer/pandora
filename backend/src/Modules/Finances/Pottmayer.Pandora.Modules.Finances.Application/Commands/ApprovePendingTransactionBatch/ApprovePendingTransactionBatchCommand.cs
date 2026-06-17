using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransactionBatch;

public sealed record ApprovePendingTransactionBatchInput(Guid UserId, IReadOnlyList<Guid> Ids);

public sealed class ApprovePendingTransactionBatchCommand(ApprovePendingTransactionBatchInput input)
    : CommandBase<ApprovePendingTransactionBatchInput, int>(input);
