using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetRules;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageRules;

/// <summary>
/// What the rule commands share. Each runs under <see cref="MailboxGates.Rules"/>, the provider first and then the row,
/// and answers with the mailbox's rules as they are now.
/// </summary>
public sealed class RuleOperation(IUnitOfWorkFactory factory, IServiceProvider services, MailboxGates gates, ISender sender)
{
    /// <summary><paramref name="work"/> answers with the error that stopped it, or null.</summary>
    public async Task<Result<IReadOnlyList<RuleDto>>> OnMailboxAsync(
        Guid userId, Guid mailboxId, Func<Mailbox, IMailboxProvider, Task<Error?>> work, CancellationToken ct)
    {
        var mailbox = await FindMailboxAsync(userId, mailboxId, ct);
        if (mailbox is null)
            return Result<IReadOnlyList<RuleDto>>.Failure(MailErrors.MailboxNotFound);

        var gate = gates.Rules(mailbox.Id);
        await gate.WaitAsync(ct);
        try
        {
            if (await work(mailbox, services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value)) is { } error)
                return Result<IReadOnlyList<RuleDto>>.Failure(error);
        }
        finally
        {
            gate.Release();
        }

        return await sender.Send(new GetRulesQuery(new GetRulesInput(userId, mailbox.Id)), ct);
    }

    /// <summary>The same, for the caller's rule, read inside the gate: a cycle may have just changed it.</summary>
    public async Task<Result<IReadOnlyList<RuleDto>>> OnRuleAsync(
        Guid userId, Guid ruleId, Func<Mailbox, MailRule, IMailboxProvider, Task<Error?>> work, CancellationToken ct)
    {
        var rule = await FindRuleAsync(ruleId, ct);
        if (rule is null || await FindMailboxAsync(userId, rule.MailboxId, ct) is null)
            return Result<IReadOnlyList<RuleDto>>.Failure(MailErrors.RuleNotFound);

        return await OnMailboxAsync(userId, rule.MailboxId, async (mailbox, provider) =>
            await FindRuleAsync(ruleId, ct) is { } current ? await work(mailbox, current, provider) : MailErrors.RuleNotFound, ct);
    }

    public Task<Dictionary<Guid, MailLabel>> LabelsAsync(Guid mailboxId, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            (await ctx.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailboxId, token)).ToDictionary(l => l.Id), cancellationToken: ct);

    public Task UpdateRuleAsync(Guid ruleId, Action<MailRule> change, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var repository = ctx.AcquireRepository<IMailRuleRepository>();
            if (await repository.GetByIdAsync(ruleId, token) is not { } rule)
                return;

            change(rule);
            await repository.UpdateAsync(rule, token);
        }, cancellationToken: ct);

    /// <summary>The actions as the provider knows them, by its own label ids.</summary>
    public static List<RemoteRuleAction> ToRemote(IEnumerable<MailRuleAction> actions, Dictionary<Guid, MailLabel> labels) =>
        actions.Select(a => new RemoteRuleAction(a.Kind, a.LabelId is { } id ? labels[id].ProviderLabelId : null)).ToList();

    private Task<Mailbox?> FindMailboxAsync(Guid userId, Guid mailboxId, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(mailboxId, userId, token), cancellationToken: ct);

    private Task<MailRule?> FindRuleAsync(Guid ruleId, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IMailRuleRepository>().GetByIdAsync(ruleId, token), cancellationToken: ct);
}
