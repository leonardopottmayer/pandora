using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransaction;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransactionBatch;

public sealed class ApprovePendingTransactionBatchCommandHandler(ISender sender)
    : CommandHandlerBase<ApprovePendingTransactionBatchCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(
        ApprovePendingTransactionBatchCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var approved = 0;

        foreach (var id in input.Ids)
        {
            var result = await sender.Send(
                new ApprovePendingTransactionCommand(new ApprovePendingTransactionInput(input.UserId, id)), ct);

            if (result.IsSuccess) approved++;
            // failures (already decided, missing amount, etc.) are silently skipped in batch mode
        }

        return Ok(approved);
    }
}
