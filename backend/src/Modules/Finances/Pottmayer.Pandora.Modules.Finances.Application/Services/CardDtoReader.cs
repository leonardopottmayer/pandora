using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>Maps one card to its DTO, reading its numbers in a unit of work of their own.</summary>
internal static class CardDtoReader
{
    public static async Task<CardDto> ReadAsync(IUnitOfWorkFactory factory, Card card, CancellationToken ct)
    {
        var numbers = await factory.ExecuteAsync(FinancesModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<ICardNumberRepository>().GetByCardsAsync([card.Id], card.UserId, token),
            cancellationToken: ct);

        return CardDto.From(card, numbers);
    }
}
