using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;

/// <summary>The reads every mailbox command shares: the caller's mailbox, and its DTO as it is now.</summary>
internal static class MailboxReads
{
    public static Task<Mailbox?> FindAsync(IUnitOfWorkFactory factory, Guid userId, Guid mailboxId, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(mailboxId, userId, token), cancellationToken: ct);

    public static Task<MailboxDto?> LoadDtoAsync(IUnitOfWorkFactory factory, Guid userId, Guid mailboxId, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var mailbox = await ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(mailboxId, userId, token);
            if (mailbox is null)
                return null;

            var counts = await ctx.AcquireRepository<IMailMessageRepository>().CountByMailboxAsync([mailbox.Id], token);
            return MailboxDto.From(mailbox, counts.GetValueOrDefault(mailbox.Id));
        }, cancellationToken: ct);
}
