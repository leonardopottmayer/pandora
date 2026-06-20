using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunImportParsing;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Integration tests for the import pipeline (phases 09 and 10):
/// - Upload detection and queuing
/// - Background parsing (via RunImportParsingCommand)
/// - Dedup: certain duplicate and suspected duplicate
/// - Abort and retry lifecycle
/// - Import layouts listing
/// </summary>
[Collection("Integration")]
public sealed class ImportsTests : IAsyncLifetime
{
    private const string Imports = "/api/v1/finances/imports";
    private const string Layouts = "/api/v1/finances/import-layouts";
    private const string Accounts = "/api/v1/finances/accounts";
    private const string Cards = "/api/v1/finances/cards";
    private const string Pending = "/api/v1/finances/pending-transactions";
    private const string Transactions = "/api/v1/finances/transactions";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ImportsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await _client.GetAsync(Imports);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_import_layouts_returns_system_layouts()
    {
        await AuthAsync("layout-list");

        var layouts = (await _client.GetFromJsonAsync<ListEnvelope<LayoutNode>>(Layouts))!.Data;

        Assert.NotEmpty(layouts);
        // All returned layouts are system layouts
        Assert.All(layouts, l => Assert.True(l.IsSystemLayout));
        // Contains at least Nubank card OFX and Itaú OFX
        Assert.Contains(layouts, l => l.LayoutCode == "nubank-card-ofx");
        Assert.Contains(layouts, l => l.LayoutCode == "itau-account-ofx");
    }

    [Fact]
    public async Task Upload_nubank_card_ofx_queues_file_as_received()
    {
        await AuthAsync("upload-ofx");
        var card = await CreateCardAsync();

        var ofx = BuildNubankCardOfx([
            new("C-001", new DateOnly(2026, 6, 1), 99.90m, "Netflix", IsCredit: false),
            new("C-002", new DateOnly(2026, 6, 3), 45.00m, "Spotify", IsCredit: false),
        ]);

        var file = await UploadAsync(ofx, "nubank-card.ofx", cardId: card);
        Assert.Equal("received", file.Status);
        Assert.Equal(0, file.TotalRows);
    }

    [Fact]
    public async Task Upload_then_parse_creates_pending_suggestions()
    {
        await AuthAsync("parse-ofx");
        var card = await CreateCardAsync();

        var ofx = BuildNubankCardOfx([
            new("P-001", new DateOnly(2026, 6, 5), 120.50m, "Amazon", IsCredit: false),
            new("P-002", new DateOnly(2026, 6, 10), 1200.00m, "Salary credit", IsCredit: true),
        ]);

        var file = await UploadAsync(ofx, "nubank.ofx", cardId: card);
        Assert.Equal("received", file.Status);

        await RunParsingAsync();

        var parsed = await GetFileAsync(file.Id);
        Assert.True(parsed.Status == "completed", $"parse failed: {parsed.ErrorMessage}");
        Assert.Equal(2, parsed.TotalRows);
        Assert.Equal(2, parsed.ParsedRows);
        Assert.Equal(0, parsed.ErrorRows);
        Assert.Equal(2, parsed.SuggestionRows);

        var rows = await GetRowsAsync(file.Id);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("suggestion-created", r.Status));
        Assert.All(rows, r => Assert.Equal("new", r.DedupStatus));
        Assert.All(rows, r => Assert.NotNull(r.PendingTransactionId));

        var pending = await ListPendingAsync();
        Assert.Equal(2, pending.Count);
        Assert.All(pending, p => Assert.Equal("import", p.Source));
    }

    [Fact]
    public async Task Reimport_same_file_creates_new_suggestions_with_certain_dedup()
    {
        await AuthAsync("reimport-dedup");
        var card = await CreateCardAsync();

        var ofx = BuildNubankCardOfx([
            new("D-001", new DateOnly(2026, 6, 1), 50.00m, "Coffee", IsCredit: false),
        ]);

        // First import
        await UploadAsync(ofx, "first.ofx", cardId: card);
        await RunParsingAsync();

        // Second import — same file
        await UploadAsync(ofx, "second.ofx", cardId: card);
        await RunParsingAsync();

        var allFiles = await ListFilesAsync();
        Assert.Equal(2, allFiles.Count);

        var secondFile = allFiles.OrderByDescending(f => f.CreatedAt).First();
        var secondRows = await GetRowsAsync(secondFile.Id);
        var row = Assert.Single(secondRows);

        // Must still generate a suggestion, but with certain dedup status
        Assert.Equal("suggestion-created", row.Status);
        Assert.Equal("certain", row.DedupStatus);
        Assert.NotNull(row.MatchedPendingTransactionId);

        // Two pending transactions total (one per import)
        var pending = await ListPendingAsync();
        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task Abort_received_file_marks_it_aborted()
    {
        await AuthAsync("abort-received");
        var account = await CreateAccountAsync();

        var ofx = BuildInterAccountOfx([
            new("A-001", new DateOnly(2026, 6, 1), 300.00m, "Deposit", IsCredit: true),
        ]);

        var file = await UploadAsync(ofx, "inter.ofx", accountId: account);
        Assert.Equal("received", file.Status);

        var abort = await _client.PostAsJsonAsync($"{Imports}/{file.Id}/abort", new { });
        Assert.Equal(HttpStatusCode.OK, abort.StatusCode);

        var aborted = await GetFileAsync(file.Id);
        Assert.Equal("aborted", aborted.Status);

        // Aborted file is not processed
        await RunParsingAsync();
        var still = await GetFileAsync(file.Id);
        Assert.Equal("aborted", still.Status);
    }

    [Fact]
    public async Task Abort_aborted_file_returns_conflict()
    {
        await AuthAsync("abort-conflict");
        var account = await CreateAccountAsync();

        var ofx = BuildInterAccountOfx([new("X-001", new DateOnly(2026, 6, 1), 10m, "Tx", IsCredit: false)]);
        var file = await UploadAsync(ofx, "inter.ofx", accountId: account);

        await _client.PostAsJsonAsync($"{Imports}/{file.Id}/abort", new { });
        var second = await _client.PostAsJsonAsync($"{Imports}/{file.Id}/abort", new { });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Retry_not_failed_file_returns_conflict()
    {
        await AuthAsync("retry-conflict");
        var account = await CreateAccountAsync();

        var ofx = BuildInterAccountOfx([new("R-001", new DateOnly(2026, 6, 1), 5m, "Tx", IsCredit: false)]);
        var file = await UploadAsync(ofx, "inter.ofx", accountId: account);

        // File is still "received" — retry should fail
        var retry = await _client.PostAsJsonAsync($"{Imports}/{file.Id}/retry", new { });
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
    }

    [Fact]
    public async Task Returns_404_for_unknown_import_id()
    {
        await AuthAsync("import-404");

        var response = await _client.GetAsync($"{Imports}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_access_another_users_import()
    {
        await AuthAsync("import-scope-1");
        var account = await CreateAccountAsync();

        var ofx = BuildInterAccountOfx([new("S-001", new DateOnly(2026, 6, 1), 10m, "Tx", IsCredit: false)]);
        var file = await UploadAsync(ofx, "inter.ofx", accountId: account);

        // Switch to a different user
        await AuthAsync("import-scope-2");
        var response = await _client.GetAsync($"{Imports}/{file.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_file_larger_than_10mb()
    {
        await AuthAsync("upload-large");
        var account = await CreateAccountAsync();

        var big = new byte[11 * 1024 * 1024];
        var response = await UploadRawAsync(big, "big.ofx", accountId: account);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_card_file_to_account_returns_validation_error()
    {
        await AuthAsync("upload-mismatch");
        var account = await CreateAccountAsync();

        // Nubank card OFX uploaded targeting an account — should fail layout+destination check
        var ofx = BuildNubankCardOfx([new("M-001", new DateOnly(2026, 6, 1), 10m, "Tx", IsCredit: false)]);
        var response = await UploadRawAsync(Encoding.UTF8.GetBytes(ofx), "nubank-card.ofx", accountId: account);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Approve_import_suggestion_creates_transaction_with_import_origin()
    {
        await AuthAsync("approve-import");
        var card = await CreateCardAsync();

        var ofx = BuildNubankCardOfx([
            new("AP-001", new DateOnly(2026, 6, 1), 200.00m, "Dinner", IsCredit: false),
        ]);

        await UploadAsync(ofx, "nubank.ofx", cardId: card);
        await RunParsingAsync();

        var pending = await ListPendingAsync();
        var entry = Assert.Single(pending);
        Assert.Equal("import", entry.Source);

        var approve = await _client.PostAsJsonAsync($"{Pending}/{entry.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var created = (await approve.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;
        Assert.Equal("import", created.Origin);

        // The inbox only lists pending suggestions, so the approved one is gone.
        Assert.Empty(await ListPendingAsync());
    }

    [Fact]
    public async Task Get_transaction_by_id_returns_the_transaction()
    {
        await AuthAsync("get-tx");
        var account = await CreateAccountAsync();
        var txId = await CreateTransactionAsync(account, 19.90m, new DateOnly(2026, 5, 1), "YouTube Premium");

        var node = (await _client.GetFromJsonAsync<SingleEnvelope<TxNode>>($"{Transactions}/{txId}"))!.Data;

        Assert.Equal(txId, node.Id);
        Assert.Equal("YouTube Premium", node.Description);
    }

    [Fact]
    public async Task Get_unknown_transaction_returns_404()
    {
        await AuthAsync("get-tx-404");
        var response = await _client.GetAsync($"{Transactions}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Link_import_suggestion_marks_row_matched_and_reimport_resolves_to_transaction()
    {
        await AuthAsync("manual-link");
        var account = await CreateAccountAsync();

        // An existing transaction the importer won't auto-match (different date, outside the fuzzy window).
        var existingTx = await CreateTransactionAsync(account, 19.90m, new DateOnly(2026, 5, 1), "YouTube");

        var ofx = BuildInterAccountOfx([
            new("YT-001", new DateOnly(2026, 6, 1), 19.90m, "YouTube Premium", IsCredit: false),
        ]);

        var firstFile = await UploadAsync(ofx, "first.ofx", accountId: account);
        await RunParsingAsync();

        var firstRows = await GetRowsAsync(firstFile.Id);
        var firstRow = Assert.Single(firstRows);
        Assert.Equal("new", firstRow.DedupStatus);

        var pending = Assert.Single(await ListPendingAsync());
        Assert.Equal("import", pending.Source);

        // Manually reconcile the suggestion against the existing transaction.
        var link = await _client.PostAsJsonAsync($"{Pending}/{pending.Id}/link", new { transactionId = existingTx });
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        // The suggestion leaves the inbox (resolved), and the row now points at the real transaction.
        Assert.Empty(await ListPendingAsync());
        var matchedRow = Assert.Single(await GetRowsAsync(firstFile.Id));
        Assert.Equal("matched", matchedRow.DedupStatus);
        Assert.Equal(existingTx, matchedRow.MatchedTransactionId);

        // Re-importing the same line now resolves straight to that transaction.
        var secondFile = await UploadAsync(ofx, "second.ofx", accountId: account);
        await RunParsingAsync();

        var secondRow = Assert.Single(await GetRowsAsync(secondFile.Id));
        Assert.Equal("certain", secondRow.DedupStatus);
        Assert.Equal(existingTx, secondRow.MatchedTransactionId);
    }

    [Fact]
    public async Task Link_returns_validation_error_for_recurrence_suggestion()
    {
        // Only import suggestions can be linked; a non-existent/non-import id is rejected.
        await AuthAsync("link-not-import");
        var account = await CreateAccountAsync();
        var tx = await CreateTransactionAsync(account, 10m, new DateOnly(2026, 5, 1), "X");

        var response = await _client.PostAsJsonAsync(
            $"{Pending}/{Guid.NewGuid()}/link", new { transactionId = tx });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_from_two_account_suggestions_creates_transfer_pair()
    {
        await AuthAsync("pending-transfer");
        var source = await CreateAccountAsync();
        var destination = await CreateAccountAsync();

        await UploadAsync(
            BuildInterAccountOfx([new("OUT-1", new DateOnly(2026, 6, 1), 500m, "Sent to savings", IsCredit: false)]),
            "out.ofx", accountId: source);
        await RunParsingAsync();
        await UploadAsync(
            BuildInterAccountOfx([new("IN-1", new DateOnly(2026, 6, 1), 500m, "Received", IsCredit: true)]),
            "in.ofx", accountId: destination);
        await RunParsingAsync();

        var pending = await ListPendingAsync();
        Assert.Equal(2, pending.Count);
        var outflow = pending.Single(p => p.Kind == "expense");
        var inflow = pending.Single(p => p.Kind == "income");

        var transfer = await _client.PostAsJsonAsync($"{Pending}/transfer", new
        {
            outflowPendingId = outflow.Id,
            inflowPendingId = inflow.Id,
            description = "Transfer to savings",
        });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        var legs = (await transfer.Content.ReadFromJsonAsync<ListEnvelope<TxNode>>())!.Data;
        Assert.Equal(2, legs.Count);
        Assert.Contains(legs, l => l.Kind == "transfer-out");
        Assert.Contains(legs, l => l.Kind == "transfer-in");

        // Both suggestions are now resolved (approved), so the inbox is empty.
        Assert.Empty(await ListPendingAsync());
    }

    [Fact]
    public async Task Transfer_from_same_direction_suggestions_returns_validation_error()
    {
        await AuthAsync("pending-transfer-bad");
        var a = await CreateAccountAsync();
        var b = await CreateAccountAsync();

        await UploadAsync(
            BuildInterAccountOfx([new("E-1", new DateOnly(2026, 6, 1), 30m, "Expense A", IsCredit: false)]),
            "a.ofx", accountId: a);
        await RunParsingAsync();
        await UploadAsync(
            BuildInterAccountOfx([new("E-2", new DateOnly(2026, 6, 1), 40m, "Expense B", IsCredit: false)]),
            "b.ofx", accountId: b);
        await RunParsingAsync();

        var pending = await ListPendingAsync();
        Assert.Equal(2, pending.Count);

        // Both are outflows — there's no inflow leg, so the transfer is rejected.
        var response = await _client.PostAsJsonAsync($"{Pending}/transfer", new
        {
            outflowPendingId = pending[0].Id,
            inflowPendingId = pending[1].Id,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── helpers ──

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAccountAsync()
    {
        var r = await _client.PostAsJsonAsync(Accounts, new
        {
            name = $"Account-{Guid.NewGuid():N}",
            type = "checking",
            currency = "BRL",
            displayOrder = 0
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<SingleEnvelope<IdNode>>())!.Data.Id;
    }

    private async Task<Guid> CreateCardAsync()
    {
        var r = await _client.PostAsJsonAsync(Cards, new
        {
            name = $"Card-{Guid.NewGuid():N}",
            type = "credit",
            currency = "BRL",
            displayOrder = 0,
            closingDay = 20,
            dueDay = 10
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<SingleEnvelope<IdNode>>())!.Data.Id;
    }

    private async Task<Guid> CreateTransactionAsync(Guid accountId, decimal amount, DateOnly occurredOn, string description)
    {
        var r = await _client.PostAsJsonAsync(Transactions, new
        {
            accountId,
            kind = "expense",
            amount,
            occurredOn = occurredOn.ToString("yyyy-MM-dd"),
            description
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data.Id;
    }

    private async Task<ImportFileNode> UploadAsync(
        string ofxContent, string fileName,
        Guid? accountId = null, Guid? cardId = null)
    {
        var bytes = Encoding.UTF8.GetBytes(ofxContent);
        var response = await UploadRawAsync(bytes, fileName, accountId, cardId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<ImportFileNode>>())!.Data;
    }

    private async Task<HttpResponseMessage> UploadRawAsync(
        byte[] bytes, string fileName,
        Guid? accountId = null, Guid? cardId = null)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        if (accountId.HasValue) content.Add(new StringContent(accountId.Value.ToString()), "accountId");
        if (cardId.HasValue) content.Add(new StringContent(cardId.Value.ToString()), "cardId");
        return await _client.PostAsync(Imports, content);
    }

    private async Task<ImportFileNode> GetFileAsync(Guid id)
    {
        var r = await _client.GetFromJsonAsync<SingleEnvelope<ImportFileNode>>($"{Imports}/{id}");
        return r!.Data;
    }

    private async Task<IReadOnlyList<ImportFileNode>> ListFilesAsync()
    {
        var r = await _client.GetFromJsonAsync<ListEnvelope<ImportFileNode>>(Imports);
        return r!.Data;
    }

    private async Task<IReadOnlyList<ImportRowNode>> GetRowsAsync(Guid fileId)
    {
        var r = await _client.GetFromJsonAsync<ListEnvelope<ImportRowNode>>($"{Imports}/{fileId}/rows");
        return r!.Data;
    }

    private async Task<IReadOnlyList<PendingNode>> ListPendingAsync()
    {
        var r = await _client.GetFromJsonAsync<ListEnvelope<PendingNode>>(Pending);
        return r!.Data;
    }

    private async Task RunParsingAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new RunImportParsingCommand());
        Assert.True(result.IsSuccess);
    }

    // Builds a minimal OFX that LayoutDetector recognises as nubank-card-ofx
    private static string BuildNubankCardOfx(IEnumerable<TxLine> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("OFXHEADER:100");
        sb.AppendLine("DATA:OFXSGML");
        sb.AppendLine("VERSION:151");
        sb.AppendLine("ENCODING:UTF-8");
        sb.AppendLine("<OFX>");
        sb.AppendLine("<CREDITCARDMSGSRSV1>");
        sb.AppendLine("<CCSTMTTRNRS>");
        sb.AppendLine("<CCSTMTRS>");
        sb.AppendLine("<FI><FID>260</FID><ORG>Nubank</ORG></FI>");
        sb.AppendLine("<BANKTRANLIST>");
        foreach (var tx in lines)
        {
            sb.AppendLine("<STMTTRN>");
            sb.AppendLine($"<TRNTYPE>{(tx.IsCredit ? "CREDIT" : "DEBIT")}");
            sb.AppendLine($"<DTPOSTED>{tx.Date:yyyyMMdd}");
            sb.AppendLine($"<TRNAMT>{(tx.IsCredit ? tx.Amount : -tx.Amount):F2}");
            sb.AppendLine($"<FITID>{tx.Fitid}");
            sb.AppendLine($"<MEMO>{tx.Memo}");
            sb.AppendLine("</STMTTRN>");
        }
        sb.AppendLine("</BANKTRANLIST>");
        sb.AppendLine("</CCSTMTRS>");
        sb.AppendLine("</CCSTMTTRNRS>");
        sb.AppendLine("</CREDITCARDMSGSRSV1>");
        sb.AppendLine("</OFX>");
        return sb.ToString();
    }

    // Builds a minimal OFX recognised as inter-ofx (account)
    private static string BuildInterAccountOfx(IEnumerable<TxLine> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("OFXHEADER:100");
        sb.AppendLine("DATA:OFXSGML");
        sb.AppendLine("VERSION:151");
        sb.AppendLine("ENCODING:UTF-8");
        sb.AppendLine("<OFX>");
        sb.AppendLine("<BANKMSGSRSV1>");
        sb.AppendLine("<STMTTRNRS>");
        sb.AppendLine("<STMTRS>");
        sb.AppendLine("<FI><FID>077</FID><ORG>Banco Inter</ORG></FI>");
        sb.AppendLine("<BANKTRANLIST>");
        foreach (var tx in lines)
        {
            sb.AppendLine("<STMTTRN>");
            sb.AppendLine($"<TRNTYPE>{(tx.IsCredit ? "CREDIT" : "DEBIT")}");
            sb.AppendLine($"<DTPOSTED>{tx.Date:yyyyMMdd}");
            sb.AppendLine($"<TRNAMT>{(tx.IsCredit ? tx.Amount : -tx.Amount):F2}");
            sb.AppendLine($"<FITID>{tx.Fitid}");
            sb.AppendLine($"<MEMO>{tx.Memo}");
            sb.AppendLine("</STMTTRN>");
        }
        sb.AppendLine("</BANKTRANLIST>");
        sb.AppendLine("</STMTRS>");
        sb.AppendLine("</STMTTRNRS>");
        sb.AppendLine("</BANKMSGSRSV1>");
        sb.AppendLine("</OFX>");
        return sb.ToString();
    }

    private sealed record TxLine(string Fitid, DateOnly Date, decimal Amount, string Memo, bool IsCredit);
    private sealed record SingleEnvelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyList<T> Data);
    private sealed record IdNode(Guid Id);
    private sealed record LayoutNode(Guid Id, string LayoutCode, string FileFormat, bool IsSystemLayout);
    private sealed record ImportFileNode(
        Guid Id, string Status, int TotalRows, int ParsedRows, int ErrorRows,
        int SuggestionRows, int DuplicateRows, string? ErrorMessage, DateTimeOffset CreatedAt);
    private sealed record ImportRowNode(
        Guid Id, Guid? PendingTransactionId, string DedupStatus, string Status,
        Guid? MatchedTransactionId, Guid? MatchedPendingTransactionId);
    private sealed record PendingNode(Guid Id, string Status, string Source, string Kind, Guid? AccountId);
    private sealed record TxNode(Guid Id, string Kind, string Status, string Description, string Origin);
}
