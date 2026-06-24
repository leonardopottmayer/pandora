using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransactionBatch;

public sealed record ApprovePendingTransactionBatchInput(Guid UserId, IReadOnlyList<Guid> Ids);

/// <summary>
/// Approves several pending suggestions in one call, one independent <c>ApprovePendingTransaction</c>
/// per id. Returns how many succeeded; any individual failure is skipped rather than aborting the batch.
/// </summary>
public sealed class ApprovePendingTransactionBatchCommand(ApprovePendingTransactionBatchInput input)
    : CommandBase<ApprovePendingTransactionBatchInput, int>(input);
