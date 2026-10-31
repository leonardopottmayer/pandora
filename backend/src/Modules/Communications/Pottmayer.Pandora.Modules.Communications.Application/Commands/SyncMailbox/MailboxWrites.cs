using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;

internal static class MailboxWrites
{
    /// <summary>Loads the mailbox in a unit of work, lets <paramref name="change"/> act, saves it; null when it is gone.</summary>
    public static async Task<Mailbox?> UpdateMailboxAsync(
        this IUnitOfWorkFactory factory, Guid mailboxId, Func<IDataContext, Mailbox, Task> change, CancellationToken ct) =>
        await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (context, token) =>
        {
            var repository = context.AcquireRepository<IMailboxRepository>();
            var mailbox = await repository.GetByIdAsync(mailboxId, token);
            if (mailbox is null)
                return null;

            await change(context, mailbox);
            await repository.UpdateAsync(mailbox, token);
            return mailbox;
        }, cancellationToken: ct);
}
