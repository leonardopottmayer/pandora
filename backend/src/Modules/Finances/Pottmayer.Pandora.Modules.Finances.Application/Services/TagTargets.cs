using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>
/// Resolves whether a tag-link target exists and belongs to the user. The link table is polymorphic
/// with no physical FK (D-tags), so this is where referential integrity is enforced. Runs inside the
/// caller's unit of work, acquiring each aggregate's repository from the same context.
/// </summary>
internal static class TagTargets
{
    public static Task<bool> ExistsForUserAsync(
        IDataContext ctx, TaggableEntityType entityType, Guid entityId, Guid userId, CancellationToken ct)
    {
        if (entityType == TaggableEntityType.Account)
            return ExistsAsync(ctx.AcquireRepository<IAccountRepository>().FindByIdForUserAsync(entityId, userId, ct));
        if (entityType == TaggableEntityType.Card)
            return ExistsAsync(ctx.AcquireRepository<ICardRepository>().FindByIdForUserAsync(entityId, userId, ct));
        if (entityType == TaggableEntityType.CardStatement)
            return ExistsAsync(ctx.AcquireRepository<ICardStatementRepository>().FindByIdForUserAsync(entityId, userId, ct));
        if (entityType == TaggableEntityType.Transaction)
            return ExistsAsync(ctx.AcquireRepository<ITransactionRepository>().FindByIdForUserAsync(entityId, userId, ct));
        if (entityType == TaggableEntityType.RecurringTransaction)
            return ExistsAsync(ctx.AcquireRepository<IRecurringTransactionRepository>().FindByIdForUserAsync(entityId, userId, ct));
        if (entityType == TaggableEntityType.PendingTransaction)
            return ExistsAsync(ctx.AcquireRepository<IPendingTransactionRepository>().FindByIdForUserAsync(entityId, userId, ct));

        return Task.FromResult(false);
    }

    private static async Task<bool> ExistsAsync<T>(Task<T?> lookup) where T : class
        => await lookup is not null;
}
