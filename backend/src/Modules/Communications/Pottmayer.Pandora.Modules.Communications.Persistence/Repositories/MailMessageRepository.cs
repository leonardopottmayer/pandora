using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Persistence.Repositories;

public sealed class MailMessageRepository(IDataContextAccessor accessor)
    : StandardRepository<MailMessage, Guid>(accessor), IMailMessageRepository
{
    private IQueryable<MailMessage> WithLabels => Set.Include(m => m.Labels);

    public Task<MailMessage?> FindByProviderIdAsync(Guid mailboxId, string providerMessageId, CancellationToken ct = default) =>
        WithLabels.FirstOrDefaultAsync(m => m.MailboxId == mailboxId && m.ProviderMessageId == providerMessageId, ct);

    public async Task<IReadOnlyList<MailMessage>> ListByProviderIdsAsync(
        Guid mailboxId, IReadOnlyCollection<string> providerMessageIds, CancellationToken ct = default) =>
        await WithLabels.Where(m => m.MailboxId == mailboxId && providerMessageIds.Contains(m.ProviderMessageId))
                        .ToListAsync(ct);

    public async Task<IReadOnlyList<(Guid Id, string ProviderMessageId)>> ListKeysUnderLabelAsync(
        Guid labelId, CancellationToken ct = default)
    {
        var rows = await Set.AsNoTracking()
                            .Where(m => m.Labels.Any(l => l.LabelId == labelId))
                            .Select(m => new { m.Id, m.ProviderMessageId })
                            .ToListAsync(ct);
        return rows.Select(r => (r.Id, r.ProviderMessageId)).ToList();
    }

    public async Task<IReadOnlyList<MailMessage>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await WithLabels.Where(m => ids.Contains(m.Id)).ToListAsync(ct);

    public Task<bool> ExistsWithRfc822IdAsync(Guid mailboxId, string rfc822MessageId, CancellationToken ct = default) =>
        Set.AnyAsync(m => m.MailboxId == mailboxId && m.Rfc822MessageId == rfc822MessageId, ct);

    public Task<int> DeleteByProviderIdsAsync(
        Guid mailboxId, IReadOnlyCollection<string> providerMessageIds, CancellationToken ct = default) =>
        Set.Where(m => m.MailboxId == mailboxId && providerMessageIds.Contains(m.ProviderMessageId)).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<MailMessage>> ListPageAsync(MailMessageQuery query, CancellationToken ct = default)
    {
        var rows = WithLabels.AsNoTracking().Where(m => query.MailboxIds.Contains(m.MailboxId));

        if (query.LabelId is { } labelId)
            rows = rows.Where(m => m.Labels.Any(l => l.LabelId == labelId));
        if (query.Unread is { } unread)
            rows = rows.Where(m => m.IsUnread == unread);
        if (query.ThreadKey is { } threadKey)
            rows = rows.Where(m => m.ThreadKey == threadKey);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            rows = rows.Where(m => EF.Functions.ILike(m.Subject, pattern)
                                   || EF.Functions.ILike(m.Snippet, pattern)
                                   || EF.Functions.ILike(m.FromAddress!, pattern)
                                   || EF.Functions.ILike(m.FromName!, pattern));
        }
        if (query.Before is { } before)
        {
            var (sentAt, id) = before;
            rows = rows.Where(m => m.SentAt < sentAt || (m.SentAt == sentAt && m.Id.CompareTo(id) < 0));
        }

        return await rows.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id).Take(query.Limit).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByMailboxAsync(
        IReadOnlyCollection<Guid> mailboxIds, CancellationToken ct = default) =>
        await Set.Where(m => mailboxIds.Contains(m.MailboxId))
                 .GroupBy(m => m.MailboxId)
                 .Select(g => new { g.Key, Count = g.Count() })
                 .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

    public async Task<IReadOnlyDictionary<Guid, (int Total, int Unread)>> CountByLabelAsync(
        Guid mailboxId, CancellationToken ct = default)
    {
        var rows = await Set.Where(m => m.MailboxId == mailboxId)
                            .SelectMany(m => m.Labels, (m, l) => new { l.LabelId, m.IsUnread })
                            .GroupBy(x => x.LabelId)
                            .Select(g => new { g.Key, Total = g.Count(), Unread = g.Count(x => x.IsUnread) })
                            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => (r.Total, r.Unread));
    }

    public Task<int> DeleteByMailboxAsync(Guid mailboxId, CancellationToken ct = default) =>
        Set.Where(m => m.MailboxId == mailboxId).ExecuteDeleteAsync(ct);

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
