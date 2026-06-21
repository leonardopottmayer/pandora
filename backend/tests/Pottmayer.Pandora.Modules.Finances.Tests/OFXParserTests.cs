using System.Text;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class OFXParserTests
{
    // Real seeded configs (migration fin012).
    private const string NubankAccount = """{ "descriptionField": "MEMO", "amountIsAlwaysAbsolute": false, "invertAmount": false, "treatPaymentAsDebit": false, "quirks": [] }""";
    private const string Inter = """{ "descriptionField": "MEMO", "amountIsAlwaysAbsolute": false, "invertAmount": false, "treatPaymentAsDebit": true, "quirks": [] }""";
    private const string NubankCard = """{ "descriptionField": "MEMO", "amountIsAlwaysAbsolute": false, "invertAmount": true, "treatPaymentAsDebit": false, "quirks": ["fitid-shared-with-secondary"] }""";
    private const string Viacredi = """{ "descriptionField": "NAME", "amountIsAlwaysAbsolute": true, "invertAmount": false, "treatPaymentAsDebit": false, "quirks": ["multiple-banktranlist", "comma-decimal", "empty-fitid"] }""";
    private const string Itau = """{ "descriptionField": "MEMO", "amountIsAlwaysAbsolute": false, "invertAmount": false, "treatPaymentAsDebit": false, "quirks": ["no-closing-tags"] }""";

    private static string Trn(string type, string amount, string fitid, string memo, string date = "20260610") =>
        $"<STMTTRN><TRNTYPE>{type}</TRNTYPE><DTPOSTED>{date}</DTPOSTED><TRNAMT>{amount}</TRNAMT><FITID>{fitid}</FITID><MEMO>{memo}</MEMO></STMTTRN>";

    private static string Ofx(params string[] trns) =>
        "OFXHEADER:100\n<OFX><BANKMSGSRSV1><STMTTRNRS><BANKTRANLIST>" + string.Concat(trns) + "</BANKTRANLIST></STMTTRNRS></BANKMSGSRSV1></OFX>";

    private static async Task<IReadOnlyList<ParsedImportRow>> Parse(string config, string ofx)
    {
        var parser = new OFXParser();
        return await parser.ParseAsync(Encoding.UTF8.GetBytes(ofx), ImportLayoutFactory.Ofx(config));
    }

    [Fact]
    public void CanParse_only_ofx_layouts()
    {
        var parser = new OFXParser();
        Assert.True(parser.CanParse(ImportLayoutFactory.Ofx(NubankAccount)));
        Assert.False(parser.CanParse(ImportLayoutFactory.Csv("{}")));
    }

    [Fact]
    public async Task Standard_signs_positive_credit_negative_debit()
    {
        var ofx = Ofx(
            Trn("CREDIT", "100.00", "f1", "Salary"),
            Trn("DEBIT", "-40.00", "f2", "Rent"));

        var rows = await Parse(NubankAccount, ofx);

        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].IsCredit);
        Assert.Equal(100m, rows[0].Amount);
        Assert.Equal(new DateOnly(2026, 6, 10), rows[0].OccurredOn);
        Assert.Equal("f1", rows[0].ExternalId);
        Assert.False(rows[1].IsCredit);
        Assert.Equal(40m, rows[1].Amount);
    }

    [Fact]
    public async Task Inter_treats_payment_type_as_debit_even_when_positive()
    {
        var ofx = Ofx(Trn("PAYMENT", "50.00", "f1", "Bill"));

        var row = Assert.Single(await Parse(Inter, ofx));

        Assert.False(row.IsCredit); // TRNTYPE=PAYMENT forced to debit
        Assert.Equal(50m, row.Amount);
    }

    [Fact]
    public async Task Nubank_card_inverts_amount_before_deciding_sign()
    {
        // Card purchases arrive as negative TRNAMT; invertAmount negates first.
        var ofx = Ofx(
            Trn("DEBIT", "-30.00", "f1", "Coffee"),     // purchase
            Trn("CREDIT", "100.00", "f2", "Payment"));   // payment to card

        var rows = await Parse(NubankCard, ofx);

        Assert.Equal(30m, rows[0].Amount);
        Assert.True(rows[0].IsCredit);   // -30 inverted → +30
        Assert.Equal(100m, rows[1].Amount);
        Assert.False(rows[1].IsCredit);  // +100 inverted → -100
    }

    [Fact]
    public async Task Viacredi_uses_trntype_for_sign_and_comma_decimal()
    {
        var ofx = Ofx(
            Trn("DEBIT", "100,50", "", "Market"),
            Trn("CREDIT", "200,00", "", "Deposit"));

        var rows = await Parse(Viacredi, ofx);

        Assert.Equal(100.50m, rows[0].Amount); // comma normalized to dot
        Assert.False(rows[0].IsCredit);
        Assert.Equal(200m, rows[1].Amount);
        Assert.True(rows[1].IsCredit);
    }

    [Fact]
    public async Task Skips_zero_amount_system_rows()
    {
        var ofx = Ofx(
            Trn("CREDIT", "0.00", "f0", "SALDO ANTERIOR"),
            Trn("DEBIT", "-10.00", "f1", "Buy"));

        var row = Assert.Single(await Parse(NubankAccount, ofx));
        Assert.Equal("Buy", row.Description);
    }

    [Fact]
    public async Task Fitid_shared_quirk_suffixes_external_id_with_description()
    {
        // Two rows sharing a FITID (main + IOF) must get distinct dedup ids.
        var ofx = Ofx(
            Trn("DEBIT", "-100.00", "same", "Purchase"),
            Trn("DEBIT", "-3.50", "same", "IOF"));

        var rows = await Parse(NubankCard, ofx);

        Assert.NotEqual(rows[0].ExternalId, rows[1].ExternalId);
        Assert.StartsWith("same:", rows[0].ExternalId);
    }

    [Fact]
    public async Task Itau_no_closing_tags_are_normalized_before_extraction()
    {
        // OFX 1.x SGML: leaf elements have no closing tag. Containers (STMTTRN) still do.
        var ofx = "OFXHEADER:100\n<OFX><BANKMSGSRSV1><STMTTRN>"
                + "<TRNTYPE>DEBIT<DTPOSTED>20260610<TRNAMT>-40.00<FITID>x1<MEMO>Rent"
                + "</STMTTRN></BANKMSGSRSV1></OFX>";

        var row = Assert.Single(await Parse(Itau, ofx));

        Assert.Equal(40m, row.Amount);
        Assert.False(row.IsCredit);
        Assert.Equal("Rent", row.Description);
        Assert.Equal("x1", row.ExternalId);
    }

    [Fact]
    public async Task Extracts_installment_marker_from_memo()
    {
        var ofx = Ofx(Trn("DEBIT", "-99.90", "f1", "Phone - Parcela 3/12"));

        var row = Assert.Single(await Parse(NubankCard, ofx));

        Assert.Equal((short)3, row.InstallmentNumber);
        Assert.Equal((short)12, row.InstallmentCount);
        Assert.Equal("Phone", row.Description);
    }
}
