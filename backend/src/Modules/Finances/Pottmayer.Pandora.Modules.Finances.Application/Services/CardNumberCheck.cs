using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>Checks the card number a transaction says it was made with.</summary>
internal static class CardNumberCheck
{
    /// <summary>
    /// The error for <paramref name="numberId"/>, or <c>null</c> when it is fine: no number, or an active
    /// number of <paramref name="cardId"/>. An archived number is accepted only when it is
    /// <paramref name="currentNumberId"/>, so editing an old transaction doesn't force a new choice.
    /// </summary>
    public static async Task<Error?> FindErrorAsync(
        IDataContext ctx, Guid? numberId, Guid cardId, Guid userId, Guid? currentNumberId, CancellationToken ct)
    {
        if (numberId is null) return null;

        var number = await ctx.AcquireRepository<ICardNumberRepository>().FindByIdForUserAsync(numberId.Value, userId, ct);
        if (number is null || number.CardId != cardId)
            return CardNumberErrors.NotOnCard;
        if (number.IsArchived && number.Id != currentNumberId)
            return CardNumberErrors.Archived;

        return null;
    }
}
