using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>The EF mappings against the com001–com006 tables: what is written reads back the same.</summary>
[Collection("Integration")]
public sealed class PersistenceTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_rule_round_trips_and_comes_back_in_position_order()
    {
        var mailbox = Mailbox.Connect(Guid.CreateVersion7(), MailProvider.Gmail, Guid.CreateVersion7(), "me@example.com", null);
        var labelId = Guid.CreateVersion7();
        var second = MailRule.Create(mailbox.Id, " Second ", 2,
            [new MailRuleCondition(RuleConditionKinds.From, "ana"), new MailRuleCondition(RuleConditionKinds.HasAttachment, "")],
            [new MailRuleAction(RuleActionKinds.AddLabel, labelId), new MailRuleAction(RuleActionKinds.MarkRead, null)],
            RuleExecution.ProviderFilter);
        second.BindFilter("filter-1");
        var first = MailRule.ImportReadOnly(mailbox.Id, "First", 1, "filter-2", """{"id": "filter-2", "action": {"forward": "bo@mail.test"}}""");

        await RunAsync(async (ctx, ct) =>
        {
            await ctx.AcquireRepository<IMailboxRepository>().AddAsync(mailbox, ct);
            var rules = ctx.AcquireRepository<IMailRuleRepository>();
            await rules.AddAsync(second, ct);
            await rules.AddAsync(first, ct);
        });

        await RunAsync(async (ctx, ct) =>
        {
            var rules = await ctx.AcquireRepository<IMailRuleRepository>().ListByMailboxAsync(mailbox.Id, ct);

            Assert.Equal(["First", "Second"], rules.Select(r => r.Name));

            Assert.True(rules[0].IsReadOnly);
            Assert.Contains("bo@mail.test", rules[0].ProviderFilterRaw);
            Assert.Empty(rules[0].Conditions);

            var saved = rules[1];
            Assert.Equal(RuleExecution.ProviderFilter, saved.Execution);
            Assert.Equal("filter-1", saved.ProviderFilterId);
            Assert.Equal(RuleStatus.Ok, saved.Status);
            Assert.True(saved.IsEnabled);
            Assert.Equal(new MailRuleCondition(RuleConditionKinds.From, "ana"), saved.Conditions[0]);
            Assert.Equal(new MailRuleAction(RuleActionKinds.AddLabel, labelId), saved.Actions[0]);
            Assert.Null(saved.Actions[1].LabelId);
        });
    }

    [Fact]
    public async Task Mailbox_label_message_and_content_round_trip()
    {
        var userId = Guid.CreateVersion7();
        var mailbox = Mailbox.Connect(userId, MailProvider.Titan, Guid.CreateVersion7(), "me@example.com", "Work");
        var inbox = MailLabel.Create(mailbox.Id, "INBOX", "Inbox", LabelKind.System, LabelRole.Inbox, isExclusive: true);
        var message = MailMessage.Create(mailbox.Id, $"{inbox.Id}:42", "<a@example.com>", "<a@example.com>",
            new MailAddress("Ana", "ana@example.com"), [new MailAddress(null, "me@example.com")], [],
            "Hello", "", DateTimeOffset.UtcNow, isUnread: true, isStarred: false, hasAttachments: true, 2048, [inbox.Id]);
        var content = MailContent.Create(message.Id, "hi", "<p>hi</p>",
            [new MailAttachment("2", "a.pdf", "application/pdf", 1024, null)], DateTimeOffset.UtcNow);

        await RunAsync(async (ctx, ct) =>
        {
            await ctx.AcquireRepository<IMailboxRepository>().AddAsync(mailbox, ct);
            await ctx.AcquireRepository<IMailLabelRepository>().AddAsync(inbox, ct);
            await ctx.AcquireRepository<IMailMessageRepository>().AddAsync(message, ct);
            await ctx.AcquireRepository<IMailContentRepository>().AddAsync(content, ct);
        });

        await RunAsync(async (ctx, ct) =>
        {
            var mailboxes = await ctx.AcquireRepository<IMailboxRepository>().ListByUserAsync(userId, ct);
            var m = Assert.Single(mailboxes);
            Assert.Equal(MailProvider.Titan, m.Provider);
            Assert.Equal(MailboxStatus.Syncing, m.Status);
            Assert.Equal(993, m.ImapPort);

            var label = Assert.Single(await ctx.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, ct));
            Assert.Equal(LabelRole.Inbox, label.Role);

            var read = await ctx.AcquireRepository<IMailMessageRepository>().FindByProviderIdAsync(mailbox.Id, $"{inbox.Id}:42", ct);
            Assert.NotNull(read);
            Assert.Equal("ana@example.com", read.FromAddress);
            Assert.Equal("me@example.com", Assert.Single(read.To).Address);
            Assert.Empty(read.Cc);
            Assert.Equal(inbox.Id, Assert.Single(read.Labels).LabelId);

            var cached = await ctx.AcquireRepository<IMailContentRepository>().GetByIdAsync(message.Id, ct);
            Assert.NotNull(cached);
            Assert.Equal("a.pdf", Assert.Single(cached.Attachments).FileName);
        });
    }

    private async Task RunAsync(Func<IDataContext, CancellationToken, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        await uow.ExecuteAsync(CommunicationsModule.DatabaseKey, work);
    }
}
