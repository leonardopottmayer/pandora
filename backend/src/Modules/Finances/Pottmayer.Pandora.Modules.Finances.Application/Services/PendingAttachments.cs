using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>
/// A suggestion's attachments follow it onto the transaction it becomes — the month's boleto, attached
/// while it waited in the inbox, ends up next to the receipt on the paid transaction. Called inside the
/// unit of work that approves (or links) the suggestion, so both commit together.
/// </summary>
internal static class PendingAttachments
{
    public static async Task MoveToTransactionAsync(
        IAttachmentRepository attachments, Guid userId, Guid pendingTransactionId, Guid transactionId, CancellationToken ct)
    {
        foreach (var attachment in await attachments.GetByOwnerAsync(userId, null, pendingTransactionId, null, ct))
        {
            attachment.MoveToTransaction(transactionId);
            await attachments.UpdateAsync(attachment, ct);
        }
    }
}
