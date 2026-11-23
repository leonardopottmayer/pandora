using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class CardNumberTests
{
    private static readonly FixedTimeProvider Time = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));

    private static CardNumber Number(CardNumberKind kind, string lastFour, Guid? cardId = null) =>
        CardNumber.Create(Guid.NewGuid(), cardId ?? Guid.NewGuid(), kind, lastFour, null, Time);

    [Theory]
    [InlineData("1234", true)]
    [InlineData(" 0042 ", true)]
    [InlineData("123", false)]
    [InlineData("12a4", false)]
    [InlineData("١٢٣٤", false)] // non-ASCII digits
    [InlineData(null, false)]
    public void IsValidLastFour_accepts_exactly_four_ascii_digits(string? value, bool expected) =>
        Assert.Equal(expected, CardNumber.IsValidLastFour(value));

    [Fact]
    public void Create_trims_and_drops_a_blank_nickname()
    {
        var number = CardNumber.Create(Guid.NewGuid(), Guid.NewGuid(), CardNumberKind.Virtual, " 9876 ", "   ", Time);

        Assert.Equal("9876", number.LastFour);
        Assert.Null(number.Nickname);
        Assert.False(number.IsArchived);
    }

    [Fact]
    public void Update_is_rejected_while_archived()
    {
        var number = Number(CardNumberKind.Virtual, "1111");
        number.Archive(Time);

        Assert.False(number.Update(CardNumberKind.Physical, "2222", "x"));
        Assert.Equal("1111", number.LastFour);
    }

    [Fact]
    public void Card_dto_names_the_card_by_its_active_physical_number()
    {
        var card = Card.Create(Guid.NewGuid(), "Nubank", null, null, 10, 20, CurrencyCode.Create("BRL"), Guid.NewGuid(), Time);
        var oldPhysical = Number(CardNumberKind.Physical, "1111", card.Id);
        oldPhysical.Archive(Time);
        var physical = Number(CardNumberKind.Physical, "2222", card.Id);
        var virtualOne = Number(CardNumberKind.Virtual, "3333", card.Id);

        var dto = CardDto.From(card, [oldPhysical, physical, virtualOne]);

        Assert.Equal("2222", dto.LastFour);
        Assert.Equal(3, dto.Numbers.Count);
    }

    [Fact]
    public void Card_dto_falls_back_to_a_virtual_number_and_then_to_none()
    {
        var card = Card.Create(Guid.NewGuid(), "Inter", null, null, 10, 20, CurrencyCode.Create("BRL"), Guid.NewGuid(), Time);

        Assert.Equal("3333", CardDto.From(card, [Number(CardNumberKind.Virtual, "3333", card.Id)]).LastFour);
        Assert.Null(CardDto.From(card, []).LastFour);
    }
}
