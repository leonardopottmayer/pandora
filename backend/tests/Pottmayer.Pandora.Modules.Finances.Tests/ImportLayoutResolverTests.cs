using System.Text;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class ImportLayoutResolverTests
{
    // System layouts carry the routing key (bank, format, account/card) plus a fallback-detectable code.
    private static readonly IReadOnlyList<ImportLayout> SystemLayouts =
    [
        ImportLayoutFactory.Routing("341", "ofx", "account", "itau-account-ofx"),
        ImportLayoutFactory.Routing("260", "ofx", "account", "nubank-account-ofx"),
        ImportLayoutFactory.Routing("260", "ofx", "card", "nubank-card-ofx"),
        ImportLayoutFactory.Routing("260", "csv", "card", "nubank-card-csv"),
    ];

    private static readonly ImportLayoutResolver Resolver = new(new LayoutDetector());

    [Fact]
    public async Task Routes_by_bank_format_and_account_type()
    {
        // Content that would sniff as Nubank, but the destination bank is Itaú → routing wins.
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1><FID>260</FID></SIGNONMSGSRSV1><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";

        var result = await Resolver.ResolveAsync(
            Encoding.UTF8.GetBytes(ofx), "extrato.ofx", bankCode: "341", isCard: false, SystemLayouts);

        Assert.True(result.IsSuccess);
        Assert.Equal("itau-account-ofx", result.Value!.LayoutCode);
    }

    [Fact]
    public async Task Routes_card_destination_to_card_layout()
    {
        var result = await Resolver.ResolveAsync(
            Encoding.UTF8.GetBytes("date,title,amount\n2026-01-01,X,1"), "fatura.csv",
            bankCode: "260", isCard: true, SystemLayouts);

        Assert.True(result.IsSuccess);
        Assert.Equal("nubank-card-csv", result.Value!.LayoutCode);
    }

    [Fact]
    public async Task Falls_back_to_content_detection_when_no_bank()
    {
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1><FID>260</FID></SIGNONMSGSRSV1><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";

        var result = await Resolver.ResolveAsync(
            Encoding.UTF8.GetBytes(ofx), "extrato.ofx", bankCode: null, isCard: false, SystemLayouts);

        Assert.True(result.IsSuccess);
        Assert.Equal("nubank-account-ofx", result.Value!.LayoutCode);
    }

    [Fact]
    public async Task Falls_back_to_content_detection_when_no_layout_for_combination()
    {
        // Itaú has no CSV account layout registered here → fall back to sniffing (which finds Nubank).
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1><FID>260</FID></SIGNONMSGSRSV1><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";

        var result = await Resolver.ResolveAsync(
            Encoding.UTF8.GetBytes(ofx), "extrato.ofx", bankCode: "341", isCard: true, SystemLayouts);

        Assert.True(result.IsSuccess);
        Assert.Equal("nubank-account-ofx", result.Value!.LayoutCode);
    }
}
