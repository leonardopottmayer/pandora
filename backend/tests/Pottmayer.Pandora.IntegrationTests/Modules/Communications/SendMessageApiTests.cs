using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pottmayer.Pandora.IntegrationTests.Modules.Integrations;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>Sending through the API, over a scripted provider (registered under <c>titan</c>).</summary>
[Collection("Integration")]
public sealed class SendMessageApiTests : IAsyncLifetime
{
    private const string Base = "/api/v1/communications";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new();
    private readonly WebApplicationFactory<Program> _host;

    public SendMessageApiTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddKeyedSingleton<IMailboxProvider>(MailProvider.Titan.Value, _provider)));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record MailboxItem(Guid Id);
    private sealed record MessageItem(Guid Id, string Subject);
    private sealed record PageItem(List<MessageItem> Items);
    private sealed record SentItem(bool CopySaved);
    private sealed record AddressItem(string? Name, string Address);
    private sealed record FileItem(string Id, string? FileName);
    private sealed record DraftItem(List<AddressItem> To, List<AddressItem> Cc, string Subject, string? InReplyTo, string OriginalText, List<FileItem> Attachments);

    [Fact]
    public async Task A_message_goes_to_the_provider_with_every_part_and_the_key_as_its_id()
    {
        var (client, mailbox) = await SignInAsync("send");
        var key = Guid.NewGuid();

        var response = await SendAsync(client, mailbox, key,
            to: ["ana@mail.test"], cc: ["bo@mail.test"], bcc: ["cy@mail.test"],
            subject: "Hello", text: "Plain", html: "<p>Rich</p>",
            file: ("notes.txt", "text/plain", "file body"u8.ToArray()));

        Assert.True((await AccountsApi.ReadDataAsync<SentItem>(response)).CopySaved);

        var (raw, threadKey) = Assert.Single(_provider.Sent);
        Assert.Null(threadKey);
        var message = Load(raw);
        Assert.Equal($"{key:N}@titan.test", message.MessageId);
        Assert.Equal("me@titan.test", message.From.Mailboxes.Single().Address);
        Assert.Equal("ana@mail.test", message.To.Mailboxes.Single().Address);
        Assert.Equal("bo@mail.test", message.Cc.Mailboxes.Single().Address);
        Assert.Equal("cy@mail.test", message.Bcc.Mailboxes.Single().Address);
        Assert.Equal("Hello", message.Subject);
        Assert.Equal("Plain", message.TextBody);
        Assert.Equal("<p>Rich</p>", message.HtmlBody);
        Assert.Equal("notes.txt", Assert.Single(message.Attachments).ContentDisposition!.FileName);
    }

    [Fact]
    public async Task The_same_key_is_never_sent_twice()
    {
        var (client, mailbox) = await SignInAsync("twice");
        var key = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, mailbox, key)).StatusCode);
        var again = await SendAsync(client, mailbox, key);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains(MailErrors.AlreadySent.Code, await again.Content.ReadAsStringAsync());
        Assert.Single(_provider.Sent);
    }

    [Fact]
    public async Task Sends_at_the_same_time_with_one_key_send_once()
    {
        var (client, mailbox) = await SignInAsync("together");
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendAsync(client, mailbox, key)));

        Assert.Single(_provider.Sent);
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task An_uncertain_send_keeps_its_key_so_a_retry_cannot_send_twice()
    {
        var (client, mailbox) = await SignInAsync("uncertain");
        var key = Guid.NewGuid();

        _provider.FailWith = MailErrors.SendUncertain;
        var uncertain = await SendAsync(client, mailbox, key);
        _provider.FailWith = null;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, uncertain.StatusCode);
        Assert.Contains(MailErrors.SendUncertain.Code, await uncertain.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, mailbox, key)).StatusCode);
        Assert.Empty(_provider.Sent);
    }

    [Fact]
    public async Task A_certain_refusal_frees_the_key_so_the_form_can_try_again()
    {
        var (client, mailbox) = await SignInAsync("refused");
        var key = Guid.NewGuid();

        _provider.FailWith = MailErrors.ProviderFailed("Recipient refused");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, key)).StatusCode);
        _provider.FailWith = null;

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, mailbox, key)).StatusCode);
        Assert.Single(_provider.Sent);
    }

    [Fact]
    public async Task A_copy_that_failed_is_still_a_success()
    {
        var (client, mailbox) = await SignInAsync("no-copy");
        _provider.CopySaved = false;

        var response = await SendAsync(client, mailbox, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await AccountsApi.ReadDataAsync<SentItem>(response)).CopySaved);
    }

    [Fact]
    public async Task A_send_answers_at_once_even_while_a_long_cycle_is_reading_the_changes()
    {
        var (client, mailbox) = await SignInAsync("during-cycle");
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);

        var inChanges = new TaskCompletionSource();
        var finishChanges = new TaskCompletionSource();
        _provider.BeforeFetchChanges = () =>
        {
            inChanges.TrySetResult();
            return finishChanges.Task;
        };
        var cycle = client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        await inChanges.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var response = await SendAsync(client, mailbox, Guid.NewGuid()).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(_provider.Sent);

        finishChanges.SetResult();
        await cycle;
    }

    [Fact]
    public async Task A_bad_request_sends_nothing()
    {
        var (client, mailbox) = await SignInAsync("bad");

        async Task Refused(params string[] to) =>
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, Guid.NewGuid(), to: to)).StatusCode);

        await Refused();
        await Refused("not an address");
        await Refused("ana@mail.test", "Bcc: evil@mail.test");
        await Refused("ana@mail.test\r\nBcc: evil@mail.test");
        await Refused(Enumerable.Range(0, 101).Select(i => $"u{i}@mail.test").ToArray());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, Guid.Empty)).StatusCode);
        Assert.Empty(_provider.Sent);
    }

    [Fact]
    public async Task Attachments_over_the_limit_send_nothing()
    {
        var (client, mailbox) = await SignInAsync("large");

        var response = await SendAsync(client, mailbox, Guid.NewGuid(), file: ("big.bin", "application/octet-stream", new byte[(25 * 1024 * 1024) + 1]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(MailErrors.MessageTooLarge.Code, await response.Content.ReadAsStringAsync());
        Assert.Empty(_provider.Sent);
    }

    [Fact]
    public async Task Someone_elses_mailbox_or_message_is_not_found_and_nothing_is_sent()
    {
        _provider.Deliver("m1", "Private");
        var (owner, mailbox) = await SignInAsync("owner");
        await owner.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        var original = await OnlyMessageAsync(owner);
        var (stranger, own) = await SignInAsync("stranger", "other@titan.test");

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(stranger, mailbox, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(stranger, own, Guid.NewGuid(), replyToId: original.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(stranger, own, Guid.NewGuid(), forwardOfId: original.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base}/messages/{original.Id}/compose?mode=reply")).StatusCode);
        Assert.Empty(_provider.Sent);
    }

    [Fact]
    public async Task A_reply_threads_on_the_original()
    {
        _provider.Deliver("m1", "Question", rfc822: "question@mail.test");
        var (client, mailbox) = await SignInAsync("reply");
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        var original = await OnlyMessageAsync(client);

        var response = await SendAsync(client, mailbox, Guid.NewGuid(), subject: "Re: Question", replyToId: original.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (raw, threadKey) = Assert.Single(_provider.Sent);
        Assert.Equal("thread-m1", threadKey);
        var message = Load(raw);
        Assert.Equal("question@mail.test", message.InReplyTo);
        Assert.Equal(["question@mail.test"], message.References);
    }

    [Fact]
    public async Task A_forward_carries_only_the_files_kept()
    {
        _provider.Deliver("m1", "Invoice");
        var (client, mailbox) = await SignInAsync("forward");
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        var original = await OnlyMessageAsync(client);

        var draft = await AccountsApi.ReadDataAsync<DraftItem>(await client.GetAsync($"{Base}/messages/{original.Id}/compose?mode=forward"));
        Assert.Equal("Fwd: Invoice", draft.Subject);
        Assert.Empty(draft.To);
        var file = Assert.Single(draft.Attachments);
        Assert.Equal("invoice.pdf", file.FileName);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, mailbox, Guid.NewGuid(), forwardOfId: original.Id, keep: [file.Id])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, mailbox, Guid.NewGuid(), forwardOfId: original.Id)).StatusCode);

        Assert.Equal("invoice.pdf", Assert.Single(Load(_provider.Sent[0].Raw).Attachments).ContentDisposition!.FileName);
        Assert.Null(_provider.Sent[0].ThreadKey);
        Assert.Empty(Load(_provider.Sent[1].Raw).Attachments);

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, mailbox, Guid.NewGuid(), forwardOfId: original.Id, keep: ["99"])).StatusCode);
        Assert.Equal(2, _provider.Sent.Count);
    }

    [Fact]
    public async Task The_form_for_a_reply_comes_from_the_raw_message()
    {
        _provider.Deliver("m1", "Question");
        var (client, mailbox) = await SignInAsync("compose");
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        var original = await OnlyMessageAsync(client);

        var draft = await AccountsApi.ReadDataAsync<DraftItem>(await client.GetAsync($"{Base}/messages/{original.Id}/compose?mode=reply"));

        Assert.Equal("ana@mail.test", Assert.Single(draft.To).Address);
        Assert.Equal("Re: Question", draft.Subject);
        Assert.Equal("m1@mail.test", draft.InReplyTo);
        Assert.Contains("Text of Question", draft.OriginalText);
        Assert.Empty(draft.Attachments);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"{Base}/messages/{original.Id}/compose?mode=nonsense")).StatusCode);
    }

    private async Task<(HttpClient Client, Guid MailboxId)> SignInAsync(string name, string address = "me@titan.test")
    {
        var client = _host.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, _factory.ConnectionString, $"{name}@example.com", name);
        var accountId = await AccountsApi.ReadDataAsync<Guid>(await client.PutAsJsonAsync(
            "/api/v1/integrations/titan/password", new { username = address, password = "secret" }));
        var mailbox = await AccountsApi.ReadDataAsync<MailboxItem>(await client.PostAsJsonAsync(
            $"{Base}/mailboxes", new { provider = "titan", externalAccountId = accountId }));
        return (client, mailbox.Id);
    }

    private static async Task<MessageItem> OnlyMessageAsync(HttpClient client) =>
        Assert.Single((await AccountsApi.ReadDataAsync<PageItem>(await client.GetAsync($"{Base}/messages"))).Items);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, Guid mailbox, Guid key, string[]? to = null, string[]? cc = null, string[]? bcc = null,
        string subject = "Hi", string text = "Body", string? html = null, Guid? replyToId = null, Guid? forwardOfId = null,
        string[]? keep = null, (string Name, string Type, byte[] Content)? file = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(mailbox.ToString()), "mailboxId" },
            { new StringContent(key.ToString()), "sendKey" },
            { new StringContent(subject), "subject" },
            { new StringContent(text), "textBody" },
        };
        foreach (var address in to ?? ["ana@mail.test"])
            form.Add(new StringContent(address), "to");
        foreach (var address in cc ?? [])
            form.Add(new StringContent(address), "cc");
        foreach (var address in bcc ?? [])
            form.Add(new StringContent(address), "bcc");
        foreach (var id in keep ?? [])
            form.Add(new StringContent(id), "keepAttachmentIds");
        if (html is not null)
            form.Add(new StringContent(html), "htmlBody");
        if (replyToId is not null)
            form.Add(new StringContent(replyToId.Value.ToString()), "replyToId");
        if (forwardOfId is not null)
            form.Add(new StringContent(forwardOfId.Value.ToString()), "forwardOfId");
        if (file is { } f)
        {
            var part = new ByteArrayContent(f.Content);
            part.Headers.ContentType = new MediaTypeHeaderValue(f.Type);
            form.Add(part, "files", f.Name);
        }

        return client.PostAsync($"{Base}/messages/send", form);
    }

    private static MimeMessage Load(byte[] raw)
    {
        using var stream = new MemoryStream(raw);
        return MimeMessage.Load(stream);
    }
}
