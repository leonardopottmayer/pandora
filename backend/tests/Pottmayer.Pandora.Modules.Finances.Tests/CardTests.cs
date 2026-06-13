using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class CardTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_fields_and_stamps_creation()
    {
        var time = new FixedTimeProvider(Now);
        var card = Card.Create(
            Guid.NewGuid(),
            "  Nubank  ",
            "  mastercard ",
            " 1234 ",
            5000m,
            10,
            20,
            CurrencyCode.Create("brl"),
            null,
            time);

        Assert.Equal("Nubank", card.Name);
        Assert.Equal("mastercard", card.Brand);
        Assert.Equal("1234", card.LastFour);
        Assert.Equal("BRL", card.Currency.Value);
        Assert.Equal(Now, card.CreatedAt);
        Assert.False(card.IsArchived);
    }

    [Fact]
    public void Update_is_rejected_while_archived()
    {
        var time = new FixedTimeProvider(Now);
        var card = Card.Create(Guid.NewGuid(), "Card", null, null, null, 10, 20, CurrencyCode.Create("BRL"), null, time);
        card.Archive(time);

        var changed = card.Update("New", null, null, 100m, 11, 21, null);

        Assert.False(changed);
        Assert.Equal("Card", card.Name);
    }
}
