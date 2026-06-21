using System.Text;
using Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class CsvParserTests
{
    // Real seeded configs (migration fin012), trimmed to what the parser reads.

    private const string NubankCardConfig = """
        {
            "delimiter": ",",
            "encoding": "UTF-8",
            "isMultiSection": false,
            "dateColumn": "date",
            "dateFormat": "yyyy-MM-dd",
            "amountColumn": "amount",
            "amountDecimalSeparator": ",",
            "descriptionColumn": "title",
            "identifierColumn": null,
            "signColumn": null,
            "amountIsAlwaysPositive": true,
            "installmentPatterns": ["- Parcela (\\d+)/(\\d+)", "(\\d+)/(\\d+)"]
        }
        """;

    private const string NubankAccountConfig = """
        {
            "delimiter": ",",
            "encoding": "UTF-8",
            "isMultiSection": false,
            "dateColumn": "Data",
            "dateFormat": "dd/MM/yyyy",
            "amountColumn": "Valor",
            "amountDecimalSeparator": ".",
            "descriptionColumn": "Descricao",
            "identifierColumn": "Identificador",
            "signColumn": null,
            "amountIsAlwaysPositive": false,
            "installmentPatterns": []
        }
        """;

    private const string ItauCardConfig = """
        {
            "delimiter": ",",
            "encoding": "UTF-8",
            "isMultiSection": false,
            "dateColumn": "data",
            "dateFormat": "yyyy-MM-dd",
            "amountColumn": "valor",
            "amountDecimalSeparator": ".",
            "descriptionColumn": "lancamento",
            "identifierColumn": null,
            "signColumn": null,
            "amountIsAlwaysPositive": false,
            "positiveAmountIsExpense": true,
            "installmentPatterns": ["(\\d+)/(\\d+)"]
        }
        """;

    private static async Task<IReadOnlyList<Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services.ParsedImportRow>> Parse(
        string config, string text)
    {
        var parser = new CsvParser();
        var layout = ImportLayoutFactory.Csv(config);
        return await parser.ParseAsync(Encoding.UTF8.GetBytes(text), layout);
    }

    [Fact]
    public void CanParse_only_csv_layouts()
    {
        var parser = new CsvParser();
        Assert.True(parser.CanParse(ImportLayoutFactory.Csv(NubankCardConfig)));
        Assert.False(parser.CanParse(ImportLayoutFactory.Ofx("{}")));
    }

    [Fact]
    public async Task Nubank_card_treats_positive_amount_as_expense_and_comma_decimal()
    {
        // Nubank card uses comma as both delimiter and decimal separator, so a decimal amount arrives
        // quoted ("12,50"); the parser strips quotes then normalizes the comma to a dot.
        var csv = "date,title,amount\n2026-06-10,Coffee,\"12,50\"\n";

        var rows = await Parse(NubankCardConfig, csv);

        var row = Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 6, 10), row.OccurredOn);
        Assert.Equal("Coffee", row.Description);
        Assert.Equal(12.50m, row.Amount);
        Assert.False(row.IsCredit); // amountIsAlwaysPositive ⇒ expense
    }

    [Fact]
    public async Task Nubank_account_standard_sign_positive_is_credit_negative_is_debit()
    {
        var csv = "Data,Valor,Identificador,Descricao\n"
                + "10/06/2026,100.00,id-1,Salary\n"
                + "11/06/2026,-40.00,id-2,Rent\n";

        var rows = await Parse(NubankAccountConfig, csv);

        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].IsCredit);
        Assert.Equal(100m, rows[0].Amount);
        Assert.Equal("id-1", rows[0].ExternalId); // identifier column → FITID
        Assert.False(rows[1].IsCredit);
        Assert.Equal(40m, rows[1].Amount);        // stored positive, direction in IsCredit
    }

    [Fact]
    public async Task Itau_card_positive_is_expense_negative_is_payment()
    {
        var csv = "data,valor,lancamento\n"
                + "2026-06-10,200.00,Store\n"
                + "2026-06-11,-50.00,Payment received\n";

        var rows = await Parse(ItauCardConfig, csv);

        Assert.Equal(2, rows.Count);
        Assert.False(rows[0].IsCredit); // positive = expense
        Assert.True(rows[1].IsCredit);  // negative = payment/refund = credit
        Assert.Equal(50m, rows[1].Amount);
    }

    [Fact]
    public async Task Extracts_installment_marker_and_cleans_description()
    {
        var csv = "date,title,amount\n2026-06-10,Phone - Parcela 3/12,99,90\n";

        var rows = await Parse(NubankCardConfig, csv);

        var row = Assert.Single(rows);
        Assert.Equal((short)3, row.InstallmentNumber);
        Assert.Equal((short)12, row.InstallmentCount);
        Assert.Equal("Phone", row.Description); // marker stripped
    }

    [Fact]
    public async Task Honors_quoted_fields_containing_the_delimiter()
    {
        var csv = "date,title,amount\n2026-06-10,\"Store, Inc\",10,00\n";

        var rows = await Parse(NubankCardConfig, csv);

        var row = Assert.Single(rows);
        Assert.Equal("Store, Inc", row.Description);
        Assert.Equal(10m, row.Amount);
    }

    [Fact]
    public async Task Skips_rows_with_zero_amount_unparseable_amount_and_bad_date()
    {
        var csv = "date,title,amount\n"
                + "2026-06-10,Zero,0,00\n"        // zero → skipped
                + "2026-06-10,Bad,abc\n"          // unparseable amount → skipped
                + "not-a-date,Bad date,10,00\n"   // bad date → skipped
                + "2026-06-12,Good,5,00\n";       // kept

        var rows = await Parse(NubankCardConfig, csv);

        var row = Assert.Single(rows);
        Assert.Equal("Good", row.Description);
        Assert.Equal(5m, row.Amount);
    }

    [Fact]
    public async Task Empty_file_yields_no_rows()
    {
        Assert.Empty(await Parse(NubankCardConfig, ""));
    }
}
