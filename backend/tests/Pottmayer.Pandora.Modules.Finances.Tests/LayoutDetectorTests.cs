using System.Text;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class LayoutDetectorTests
{
    private static readonly IReadOnlyList<ImportLayout> SystemLayouts =
    [
        ImportLayoutFactory.WithCode("nubank-card-ofx"),
        ImportLayoutFactory.WithCode("viacredi-ofx"),
        ImportLayoutFactory.WithCode("nubank-account-ofx"),
        ImportLayoutFactory.WithCode("inter-ofx"),
        ImportLayoutFactory.WithCode("itau-account-ofx"),
        ImportLayoutFactory.WithCode("viacredi-account-csv", "csv"),
        ImportLayoutFactory.WithCode("nubank-card-csv", "csv"),
        ImportLayoutFactory.WithCode("nubank-account-csv", "csv"),
        ImportLayoutFactory.WithCode("itau-card-csv", "csv"),
    ];

    private static async Task<string?> Detect(string content, string fileName)
    {
        var detector = new LayoutDetector();
        var result = await detector.DetectAsync(Encoding.UTF8.GetBytes(content), fileName, SystemLayouts);
        return result.IsSuccess ? result.Value!.LayoutCode : null;
    }

    // ─── OFX detection order ──────────────────────────────────────────────────

    [Fact]
    public async Task Nubank_card_ofx_detected_by_creditcard_wrapper_and_fid()
    {
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1><FID>260</FID></SIGNONMSGSRSV1><CREDITCARDMSGSRSV1></CREDITCARDMSGSRSV1></OFX>";
        Assert.Equal("nubank-card-ofx", await Detect(ofx, "fatura.ofx"));
    }

    [Fact]
    public async Task Viacredi_ofx_detected_by_bankinfo()
    {
        var ofx = "OFXHEADER:100\n<OFX><BANKMSGSRSV1><BANKINFO></BANKINFO></BANKMSGSRSV1></OFX>";
        Assert.Equal("viacredi-ofx", await Detect(ofx, "extrato.ofx"));
    }

    [Fact]
    public async Task Nubank_account_ofx_detected_by_fid_260_without_card_wrapper()
    {
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1><FID>260</FID></SIGNONMSGSRSV1><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";
        Assert.Equal("nubank-account-ofx", await Detect(ofx, "extrato.ofx"));
    }

    [Fact]
    public async Task Inter_ofx_detected_by_org_name()
    {
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1><ORG>Intermedium</ORG></SIGNONMSGSRSV1><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";
        Assert.Equal("inter-ofx", await Detect(ofx, "extrato.ofx"));
    }

    [Fact]
    public async Task Itau_account_ofx_detected_by_bankid()
    {
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1></SIGNONMSGSRSV1><BANKMSGSRSV1><BANKID>0341</BANKID></BANKMSGSRSV1></OFX>";
        Assert.Equal("itau-account-ofx", await Detect(ofx, "extrato.ofx"));
    }

    // ─── CSV detection ────────────────────────────────────────────────────────

    [Fact]
    public async Task Nubank_card_csv_detected_by_exact_header()
    {
        var csv = "date,title,amount\n2026-06-10,Coffee,12.50\n";
        Assert.Equal("nubank-card-csv", await Detect(csv, "nubank.csv"));
    }

    [Fact]
    public async Task Nubank_account_csv_detected_by_identificador_column()
    {
        var csv = "Data,Valor,Identificador,Descrição\n10/06/2026,100.00,uuid-1,Salary\n";
        Assert.Equal("nubank-account-csv", await Detect(csv, "extrato.csv"));
    }

    [Fact]
    public async Task Itau_card_csv_detected_by_data_lancamento_valor()
    {
        var csv = "data,lançamento,valor\n2026-06-10,Store,200.00\n";
        Assert.Equal("itau-card-csv", await Detect(csv, "fatura.csv"));
    }

    [Fact]
    public async Task Viacredi_account_csv_detected_by_conta_prefix()
    {
        var csv = "Conta;12345\nData do Extrato;...\nData;Histórico;Valor\n";
        Assert.Equal("viacredi-account-csv", await Detect(csv, "extrato.csv"));
    }

    // ─── Failure cases ────────────────────────────────────────────────────────

    [Fact]
    public async Task Unknown_csv_header_is_not_detected()
    {
        var csv = "foo,bar,baz\n1,2,3\n";
        Assert.Null(await Detect(csv, "mystery.csv"));
    }

    [Fact]
    public async Task Unknown_ofx_layout_is_not_detected()
    {
        var ofx = "OFXHEADER:100\n<OFX><SIGNONMSGSRSV1><FID>999</FID></SIGNONMSGSRSV1><BANKMSGSRSV1></BANKMSGSRSV1></OFX>";
        Assert.Null(await Detect(ofx, "extrato.ofx"));
    }

    [Fact]
    public async Task Detected_layout_missing_from_system_list_fails()
    {
        var detector = new LayoutDetector();
        var ofx = "OFXHEADER:100\n<OFX><BANKMSGSRSV1><BANKINFO></BANKINFO></BANKMSGSRSV1></OFX>";

        // System list without viacredi-ofx → detection finds the code but can't resolve it.
        var result = await detector.DetectAsync(
            Encoding.UTF8.GetBytes(ofx), "extrato.ofx", [ImportLayoutFactory.WithCode("nubank-card-ofx")]);

        Assert.True(result.IsFailure);
    }
}
