using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>
/// The cash side of a position movement (INV-7): the ledger entry it creates on the account, and the
/// void when the movement is undone. Entries are created here directly, so the rule that limits these
/// kinds to investment accounts, which guards hand-typed entries, does not apply (INV-8).
/// </summary>
internal static class PositionLedger
{
    public const string UndoReason = "Investment movement undone";

    /// <summary>Creates and links the movement's ledger entry, when the movement moves money.</summary>
    public static async Task LinkAsync(
        IDataContext ctx, Account account, Asset asset, PositionMovement movement, DateOnly today,
        TimeProvider timeProvider, Guid correlationId, CancellationToken ct)
    {
        if (movement.LedgerKind is not { } kind || movement.NetAmount is not > 0m) return;

        var label = asset.Code ?? asset.Name;
        var transaction = Transaction.CreateAccountTransaction(
            movement.UserId, account.Id, kind, account.Currency, movement.NetAmount.Value, movement.OccurredOn,
            description: $"Investment {movement.Kind.Value}: {label}", payee: null, notes: movement.Notes,
            systemCategoryId: null, userCategoryId: null, post: movement.OccurredOn <= today, timeProvider,
            systemDescription: SystemDescription.Investment(movement.Kind.Value, label));

        await ctx.AcquireRepository<ITransactionRepository>().AddAsync(transaction, ct);
        movement.LinkTransaction(transaction.Id);

        await ctx.RecordAsync(
            movement.UserId, movement.UserId, TransactionEvents.EntityType, transaction.Id, TransactionEvents.Created,
            timeProvider.GetUtcNow(),
            new
            {
                accountId = account.Id,
                kind = transaction.Kind.Value,
                status = transaction.Status.Value,
                amount = transaction.Amount,
                currency = transaction.Currency.Value,
                positionMovementId = movement.Id
            },
            correlationId, ct);
    }

    /// <summary>Voids the movement's ledger entry, if it has one that is not void yet.</summary>
    public static async Task VoidAsync(
        IDataContext ctx, PositionMovement movement, TimeProvider timeProvider, Guid correlationId, CancellationToken ct)
    {
        if (movement.TransactionId is null) return;

        var transactions = ctx.AcquireRepository<ITransactionRepository>();
        var transaction = await transactions.FindByIdForUserAsync(movement.TransactionId.Value, movement.UserId, ct);
        if (transaction is null || !transaction.Void(UndoReason, timeProvider)) return;

        await transactions.UpdateAsync(transaction, ct);
        await ctx.RecordAsync(
            movement.UserId, movement.UserId, TransactionEvents.EntityType, transaction.Id, TransactionEvents.Voided,
            timeProvider.GetUtcNow(), new { reason = UndoReason, positionMovementId = movement.Id }, correlationId, ct);
    }
}
