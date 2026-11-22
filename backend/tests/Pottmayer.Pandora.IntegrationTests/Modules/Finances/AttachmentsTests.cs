using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Files;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers files attached to transactions: the round-trip on a transaction, the month's boleto on a
/// recurrence's suggestion following it onto the approved transaction, the type/owner guards, another
/// user's attachment staying out of reach, the bytes landing in finances' own blob table, and a file shared
/// with the assistant bot waiting in the queue until it is filed.
/// </summary>
[Collection("Integration")]
public sealed class AttachmentsTests : IAsyncLifetime
{
    private const string Url = "/api/v1/finances/attachments";
    private const string Accounts = "/api/v1/finances/accounts";
    private const string Transactions = "/api/v1/finances/transactions";
    private const string RecurringTransactions = "/api/v1/finances/recurring-transactions";
    private const string PendingTransactions = "/api/v1/finances/pending-transactions";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Pdf = "%PDF-1.4 not really a pdf"u8.ToArray();

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AttachmentsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_receipt_on_a_transaction_round_trips_and_is_counted_and_deleted()
    {
        await AuthAsync("fin-attach1");
        var account = await CreateAccountAsync();
        var tx = await CreateTransactionAsync(account);

        var (status, attachment) = await UploadAsync(Png, "pix.png", "image/png", "receipt", transactionId: tx);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("receipt", attachment!.Kind);
        Assert.Equal(tx, attachment.TransactionId);

        var download = await _client.GetAsync(attachment.Url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());

        Assert.Single(await ListAsync($"transactionId={tx}"));
        var listed = (await _client.GetFromJsonAsync<ListEnvelope<TxNode>>($"{Transactions}?accountId={account}"))!.Data;
        Assert.Equal(1, Assert.Single(listed).AttachmentCount);

        Assert.Equal(1, await CountBlobsAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync(attachment.Url)).StatusCode);
        Assert.Empty(await ListAsync($"transactionId={tx}"));
        Assert.Equal(0, await CountBlobsAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(attachment.Url)).StatusCode);
    }

    [Fact]
    public async Task The_boleto_on_a_suggestion_moves_to_the_transaction_when_it_is_approved()
    {
        await AuthAsync("fin-attach2");
        var account = await CreateAccountAsync();
        await SeedRecurringAndGenerateAsync(account, new DateOnly(2026, 6, 1));
        var pending = Assert.Single((await _client.GetFromJsonAsync<ListEnvelope<PendingNode>>(PendingTransactions))!.Data);

        var (status, _) = await UploadAsync(Pdf, "boleto-junho.pdf", "application/pdf", "bill", pendingTransactionId: pending.Id);
        Assert.Equal(HttpStatusCode.OK, status);
        var inbox = (await _client.GetFromJsonAsync<ListEnvelope<PendingNode>>(PendingTransactions))!.Data;
        Assert.Equal(1, Assert.Single(inbox).AttachmentCount);

        var approve = await _client.PostAsJsonAsync($"{PendingTransactions}/{pending.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var tx = (await approve.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;

        Assert.Empty(await ListAsync($"pendingTransactionId={pending.Id}"));
        var moved = Assert.Single(await ListAsync($"transactionId={tx.Id}"));
        Assert.Equal("bill", moved.Kind);

        // The receipt joins it once paid.
        await UploadAsync(Png, "comprovante.png", "image/png", "receipt", transactionId: tx.Id);
        Assert.Equal(["bill", "receipt"], (await ListAsync($"transactionId={tx.Id}")).Select(a => a.Kind));
    }

    [Fact]
    public async Task Only_images_and_pdfs_with_a_known_kind_on_at_most_one_owner_are_accepted()
    {
        await AuthAsync("fin-attach3");
        var account = await CreateAccountAsync();
        var tx = await CreateTransactionAsync(account);

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await UploadAsync("PK"u8.ToArray(), "x.zip", "application/zip", "receipt", transactionId: tx)).status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await UploadAsync(Png, "x.png", "image/png", "selfie", transactionId: tx)).status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await UploadAsync(Png, "x.png", "image/png", "receipt", transactionId: tx, pendingTransactionId: Guid.NewGuid())).status);
        Assert.Equal(HttpStatusCode.NotFound,
            (await UploadAsync(Png, "x.png", "image/png", "receipt", transactionId: Guid.NewGuid())).status);
    }

    [Fact]
    public async Task Another_users_attachment_is_not_found()
    {
        await AuthAsync("fin-attach4");
        var account = await CreateAccountAsync();
        var tx = await CreateTransactionAsync(account);
        var (_, attachment) = await UploadAsync(Png, "pix.png", "image/png", "receipt", transactionId: tx);

        await AuthAsync("fin-attach5");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(attachment!.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(attachment.Url)).StatusCode);
        Assert.Empty(await ListAsync($"transactionId={tx}"));
    }

    [Fact]
    public async Task A_file_shared_with_the_bot_waits_in_the_queue_until_it_is_filed_once()
    {
        await AuthAsync("fin-attach6");
        var account = await CreateAccountAsync();
        var tx = await CreateTransactionAsync(account);
        var userId = await UserIdAsync("fin-attach6@example.com");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetServices<IAssistantFileQueue>().Single();
            Assert.True(await queue.EnqueueAsync(
                new SharedFile(userId, "pix.pdf", "application/pdf", Pdf, "Comprovante luz setembro")));
        }

        var queued = Assert.Single(await ListAsync("queued=true"));
        Assert.Equal(("receipt", "Comprovante luz setembro"), (queued.Kind, queued.Note));
        Assert.Null(queued.TransactionId);

        Assert.Equal(HttpStatusCode.NotFound,
            (await AssignAsync(queued.Id, new { cardStatementId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(queued.Id, new { transactionId = tx })).StatusCode);

        Assert.Empty(await ListAsync("queued=true"));
        Assert.Equal(queued.Id, Assert.Single(await ListAsync($"transactionId={tx}")).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await AssignAsync(queued.Id, new { transactionId = tx })).StatusCode);
    }

    // ── helpers ──

    private Task<HttpResponseMessage> AssignAsync(Guid id, object owner) =>
        _client.PostAsJsonAsync($"{Url}/{id}/assign", owner);

    private async Task<Guid> UserIdAsync(string email)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM identity.idt001_user WHERE email = $1", conn);
        cmd.Parameters.AddWithValue(email);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAccountAsync()
    {
        var response = await _client.PostAsJsonAsync(Accounts, new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<IdNode>>())!.Data.Id;
    }

    private async Task<Guid> CreateTransactionAsync(Guid accountId)
    {
        var response = await _client.PostAsJsonAsync(Transactions, new
        {
            accountId,
            kind = "expense",
            amount = 30m,
            occurredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            description = "Groceries"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<IdNode>>())!.Data.Id;
    }

    private async Task SeedRecurringAndGenerateAsync(Guid accountId, DateOnly startDate)
    {
        var response = await _client.PostAsJsonAsync(RecurringTransactions, new
        {
            name = "Condomínio",
            accountId,
            kind = "expense",
            amount = 800m,
            amountIsEstimate = false,
            description = "Condomínio",
            frequency = "monthly",
            interval = 1,
            startDate,
            autoPost = false
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new RunRecurrenceGenerationCommand(new RunRecurrenceGenerationInput(startDate, 0)));
        Assert.True(result.IsSuccess);
    }

    private async Task<(HttpStatusCode status, AttachmentNode? dto)> UploadAsync(
        byte[] content, string fileName, string contentType, string kind,
        Guid? transactionId = null, Guid? pendingTransactionId = null)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(kind), "kind");
        if (transactionId is { } tx)
            form.Add(new StringContent(tx.ToString()), "transactionId");
        if (pendingTransactionId is { } pending)
            form.Add(new StringContent(pending.ToString()), "pendingTransactionId");

        var response = await _client.PostAsync(Url, form);
        if (response.StatusCode != HttpStatusCode.OK)
            return (response.StatusCode, null);
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<SingleEnvelope<AttachmentNode>>())!.Data);
    }

    private async Task<IReadOnlyList<AttachmentNode>> ListAsync(string query) =>
        (await _client.GetFromJsonAsync<ListEnvelope<AttachmentNode>>($"{Url}?{query}"))!.Data;

    private async Task<long> CountBlobsAsync()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM finances.fin018_file_blob", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private sealed record SingleEnvelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyList<T> Data);
    private sealed record IdNode(Guid Id);
    private sealed record TxNode(Guid Id, int AttachmentCount);
    private sealed record PendingNode(Guid Id, int AttachmentCount);
    private sealed record AttachmentNode(
        Guid Id, Guid? TransactionId, Guid? PendingTransactionId, string Kind, string? Note, string Url);
}
