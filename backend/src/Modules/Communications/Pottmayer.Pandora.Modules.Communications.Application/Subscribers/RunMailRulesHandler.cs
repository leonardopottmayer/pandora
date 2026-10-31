using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ApplyMessageAction;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Communications.Contracts;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.Services;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Subscribers;

/// <summary>
/// Pandora's own rule engine: a message arrived, so the mailbox's <c>pandora</c> rules run on it, through the same path as
/// a user's action. A Gmail mailbox has none, since its rules are filters that run at Google.
/// </summary>
/// <remarks>
/// The outbox delivers at least once and retries what throws, so what is already done is skipped: a redelivered event finds
/// the message in its folder and does not move it again. A failure the provider may get over throws, for the outbox to
/// retry; any other would only fail again, so it just stays on the rule. Either way the sync is not involved.
/// ponytail: a retry minutes later moves again a message the user moved back meanwhile; a table of handled event ids if that
/// ever shows up.
/// </remarks>
public sealed class RunMailRulesHandler(IUnitOfWorkFactory factory, IServiceProvider services, ISender sender)
    : IIntegrationEventHandler<MailMessageReceived>
{
    public async Task HandleAsync(MailMessageReceived @event, CancellationToken cancellationToken = default)
    {
        var state = await LoadAsync(@event, cancellationToken);
        if (state is null)
            return;

        string? text = null;
        var waitingForText = state.Rules.Where(r => RuleMatcher.NeedsText(r, state.Message)).ToList();
        if (waitingForText.Count > 0)
        {
            var raw = await services.GetRequiredKeyedService<IMailboxProvider>(state.Mailbox.Provider.Value)
                .GetRawMessageAsync(state.Mailbox, state.Message.ProviderMessageId, cancellationToken);
            if (raw.IsFailure)
            {
                await FinishAsync(waitingForText, raw.Errors[0], cancellationToken);
                return;
            }

            text = MimeReader.PlainText(MimeReader.Load(raw.Value));
        }

        var matching = state.Rules.Where(r => RuleMatcher.Matches(r, state.Message, text)).ToList();
        if (matching.Count == 0)
            return;

        var failure = await ApplyAsync(@event, state, RuleMatcher.Plan(matching), cancellationToken);

        // The message left while the rules ran, so there is nothing to act on and nothing wrong with the rules.
        if (failure?.Code == MailErrors.MessageNotFound.Code)
            return;

        await FinishAsync(matching, failure, cancellationToken);
    }

    private Task<State?> LoadAsync(MailMessageReceived @event, CancellationToken ct) =>
        factory.ExecuteAsync<State?>(CommunicationsModule.DatabaseKey, async (ctx, token) =>
        {
            var rules = (await ctx.AcquireRepository<IMailRuleRepository>().ListByMailboxAsync(@event.MailboxId, token))
                .Where(r => r.RunsInPandora)
                .ToList();
            if (rules.Count == 0)
                return null;

            var mailbox = await ctx.AcquireRepository<IMailboxRepository>().GetByIdAsync(@event.MailboxId, token);
            var message = (await ctx.AcquireRepository<IMailMessageRepository>().ListByIdsAsync([@event.MessageId], token)).FirstOrDefault();
            if (mailbox is null || message is null)
                return null;

            return new State(mailbox, message, rules, await ctx.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, token));
        }, cancellationToken: ct);

    /// <summary>
    /// Flags and labels first, then where the message goes, since a move can change its provider id. Stops at the first failure.
    /// </summary>
    private async Task<Error?> ApplyAsync(MailMessageReceived @event, State state, RulePlan plan, CancellationToken ct)
    {
        foreach (var (action, labelId) in Steps(state, plan))
        {
            var result = await sender.Send(new ApplyMessageActionCommand(new ApplyMessageActionInput(
                @event.UserId, @event.MailboxId, [@event.MessageId], action, labelId)), ct);
            if (result.IsFailure)
                return result.Errors[0];
        }

        return null;
    }

    private static IEnumerable<(string Action, Guid? LabelId)> Steps(State state, RulePlan plan)
    {
        var message = state.Message;
        var current = message.Labels.Select(l => l.LabelId).ToHashSet();
        bool IsIn(LabelRole role) => state.Labels.Any(l => l.Role == role && current.Contains(l.Id));

        if (plan.MarkRead && message.IsUnread)
            yield return (MessageActions.MarkRead, null);
        if (plan.Star && !message.IsStarred)
            yield return (MessageActions.Star, null);

        foreach (var label in plan.AddLabels.Where(id => !current.Contains(id)))
            yield return (MessageActions.AddLabel, label);

        if (plan.Trash && !IsIn(LabelRole.Trash))
            yield return (MessageActions.Trash, null);
        if (plan.MoveTo is { } target && !current.Contains(target))
            yield return (MessageActions.Move, target);
        if (plan.Archive && !IsIn(LabelRole.Archive))
            yield return (MessageActions.Archive, null);
    }

    /// <summary>The error goes on the rules, or a success clears the last one; then a failure worth retrying throws.</summary>
    private async Task FinishAsync(IReadOnlyList<MailRule> rules, Error? failure, CancellationToken ct)
    {
        if (failure is not null || rules.Any(r => r.LastError is not null))
        {
            var ids = rules.Select(r => r.Id).ToHashSet();
            await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            {
                var repository = ctx.AcquireRepository<IMailRuleRepository>();
                foreach (var rule in (await repository.ListByMailboxAsync(rules[0].MailboxId, token)).Where(r => ids.Contains(r.Id)))
                {
                    if (failure is null)
                        rule.ClearError();
                    else
                        rule.RecordError(failure.Message);
                    await repository.UpdateAsync(rule, token);
                }
            }, cancellationToken: ct);
        }

        if (failure?.Code == MailErrors.MailboxAuthFailed(string.Empty).Code || failure?.Code == MailErrors.ProviderFailed(string.Empty).Code)
            throw new InvalidOperationException($"Mail rules failed and will be retried: {failure.Message}");
    }

    private sealed record State(Mailbox Mailbox, MailMessage Message, IReadOnlyList<MailRule> Rules, IReadOnlyList<MailLabel> Labels);
}
