using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;

internal static class MessageReads
{
    /// <summary>The message and its mailbox, or null when either is missing or the mailbox is someone else's.</summary>
    public static async Task<(MailMessage Message, Mailbox Mailbox)?> FindAsync(
        IDataContext ctx, Guid userId, Guid messageId, CancellationToken ct)
    {
        var message = (await ctx.AcquireRepository<IMailMessageRepository>().ListByIdsAsync([messageId], ct)).FirstOrDefault();
        if (message is null)
            return null;

        var mailbox = await ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(message.MailboxId, userId, ct);
        return mailbox is null ? null : (message, mailbox);
    }
}
