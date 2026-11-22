using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Dtos;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ApplyMessageAction;

/// <remarks>
/// Under the action gate, so it cannot interleave with a cycle's changes. The local change only anticipates what the
/// next cycle reports anyway, so a provider that says less (a move with no new id) just leaves the row to that cycle.
/// </remarks>
public sealed class ApplyMessageActionCommandHandler(
    IUnitOfWorkFactory factory,
    IServiceProvider services,
    MailboxGates gates) : CommandHandlerBase<ApplyMessageActionCommand, IReadOnlyList<MessageSummaryDto>>
{
    private static readonly string[] Known =
    [
        MessageActions.MarkRead, MessageActions.MarkUnread, MessageActions.Star, MessageActions.Unstar, MessageActions.Move,
        MessageActions.Archive, MessageActions.Trash, MessageActions.AddLabel, MessageActions.RemoveLabel,
    ];

    protected override async Task<Result<IReadOnlyList<MessageSummaryDto>>> HandleAsync(ApplyMessageActionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var ids = input.MessageIds.Distinct().ToList();
        if (ids.Count is 0 or > MessageActions.MaxMessages)
            return Fail(MailErrors.MessageCountInvalid);
        if (!Known.Contains(input.Action))
            return Fail(MailErrors.ActionNotSupported);

        var gate = gates.Action(input.MailboxId);
        await gate.WaitAsync(ct);
        try
        {
            var state = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            {
                var mailbox = await ctx.AcquireRepository<IMailboxRepository>().FindForUserAsync(input.MailboxId, input.UserId, token);
                return mailbox is null
                    ? null
                    : new State(
                        mailbox,
                        await ctx.AcquireRepository<IMailMessageRepository>().ListByIdsAsync(ids, token),
                        await ctx.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, token));
            }, cancellationToken: ct);

            if (state is null)
                return Fail(MailErrors.MailboxNotFound);
            if (state.Messages.Count != ids.Count || state.Messages.Any(m => m.MailboxId != state.Mailbox.Id))
                return Fail(MailErrors.MessageNotFound);

            var provider = services.GetRequiredKeyedService<IMailboxProvider>(state.Mailbox.Provider.Value);
            var change = await CallProviderAsync(input, state, provider, ct);
            if (change.IsFailure)
                return Result<IReadOnlyList<MessageSummaryDto>>.Failure(change.Errors);

            return Ok(await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, token) =>
            {
                var messages = ctx.AcquireRepository<IMailMessageRepository>();
                var tracked = await messages.ListByIdsAsync(ids, token);
                foreach (var message in tracked)
                {
                    change.Value(message);
                    await messages.UpdateAsync(message, token);
                }

                return (IReadOnlyList<MessageSummaryDto>)tracked.Select(MessageSummaryDto.FromMessage).ToList();
            }, cancellationToken: ct));
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>What the provider accepted, as the change to make on each local row.</summary>
    private static async Task<Result<Action<MailMessage>>> CallProviderAsync(
        ApplyMessageActionInput input, State state, IMailboxProvider provider, CancellationToken ct)
    {
        var ids = state.Messages.Select(m => m.ProviderMessageId).ToList();

        switch (input.Action)
        {
            case MessageActions.MarkRead:
                return await SetFlagsAsync(provider, state, ids, false, null, ct);
            case MessageActions.MarkUnread:
                return await SetFlagsAsync(provider, state, ids, true, null, ct);
            case MessageActions.Star:
                return await SetFlagsAsync(provider, state, ids, null, true, ct);
            case MessageActions.Unstar:
                return await SetFlagsAsync(provider, state, ids, null, false, ct);

            case MessageActions.Move:
                if (state.Label(input.LabelId) is not { } target)
                    return MailErrors.LabelNotFound;
                if (!CanMoveTo(target))
                    return MailErrors.SystemLabel;
                return Moved(await provider.MoveAsync(state.Mailbox, ids, target.ProviderLabelId, ct), state, target);

            case MessageActions.Archive when provider.Capabilities.MultipleLabels:
                // Gmail has no archive label: archived is "not in the inbox".
                if (state.Labels.FirstOrDefault(l => l.Role == LabelRole.Inbox) is not { } inbox)
                    return Result<Action<MailMessage>>.Success(_ => { });
                return Changed(await provider.ChangeLabelsAsync(state.Mailbox, ids, [], [inbox.ProviderLabelId], ct), m => m.RemoveLabel(inbox.Id));

            case MessageActions.Archive:
                if (state.Labels.FirstOrDefault(l => l.Role == LabelRole.Archive) is not { } archive)
                    return MailErrors.NoArchiveFolder;
                return Moved(await provider.MoveAsync(state.Mailbox, ids, archive.ProviderLabelId, ct), state, archive);

            case MessageActions.Trash:
                return Trashed(await provider.TrashAsync(state.Mailbox, ids, ct), state);

            case MessageActions.AddLabel or MessageActions.RemoveLabel:
                if (!provider.Capabilities.MultipleLabels)
                    return MailErrors.ActionNotSupported;
                if (state.Label(input.LabelId) is not { } label)
                    return MailErrors.LabelNotFound;
                if (label.Kind != LabelKind.User)
                    return MailErrors.SystemLabel;

                return input.Action == MessageActions.AddLabel
                    ? Changed(await provider.ChangeLabelsAsync(state.Mailbox, ids, [label.ProviderLabelId], [], ct),
                        m => m.SetLabels(m.Labels.Select(l => l.LabelId).Append(label.Id)))
                    : Changed(await provider.ChangeLabelsAsync(state.Mailbox, ids, [], [label.ProviderLabelId], ct),
                        m => m.RemoveLabel(label.Id));

            default:
                return MailErrors.ActionNotSupported;
        }
    }

    private static async Task<Result<Action<MailMessage>>> SetFlagsAsync(
        IMailboxProvider provider, State state, IReadOnlyCollection<string> ids, bool? isUnread, bool? isStarred, CancellationToken ct) =>
        Changed(await provider.SetFlagsAsync(state.Mailbox, ids, isUnread, isStarred, ct), m => m.SetFlags(isUnread, isStarred));

    private static Result<Action<MailMessage>> Changed(Result<bool> result, Action<MailMessage> change) =>
        result.IsSuccess ? Result<Action<MailMessage>>.Success(change) : Result<Action<MailMessage>>.Failure(result.Errors);

    private static Result<Action<MailMessage>> Moved(
        Result<IReadOnlyDictionary<string, string?>> result, State state, MailLabel target) =>
        !result.IsSuccess
            ? Result<Action<MailMessage>>.Failure(result.Errors)
            : Result<Action<MailMessage>>.Success(m =>
            {
                if (result.Value.GetValueOrDefault(m.ProviderMessageId) is not { } next)
                    return;

                m.Rebind(next);
                var inbox = state.Labels.FirstOrDefault(l => l.Role == LabelRole.Inbox)?.Id;
                m.SetLabels(target.IsExclusive
                    ? [target.Id]
                    : m.Labels.Select(l => l.LabelId).Where(id => id != inbox).Append(target.Id));
            });

    /// <summary>The provider's trash, on either kind of provider, is the message's only place from then on.</summary>
    private static Result<Action<MailMessage>> Trashed(Result<IReadOnlyDictionary<string, string?>> result, State state)
    {
        if (!result.IsSuccess)
            return Result<Action<MailMessage>>.Failure(result.Errors);

        var trash = state.Labels.FirstOrDefault(l => l.Role == LabelRole.Trash);
        return Result<Action<MailMessage>>.Success(m =>
        {
            if (result.Value.GetValueOrDefault(m.ProviderMessageId) is not { } next)
                return;

            m.Rebind(next);
            if (trash is not null)
                m.SetLabels([trash.Id]);
        });
    }

    /// <summary>Nothing moves into Sent or Drafts. On Gmail, of the system labels only the inbox can be a target.</summary>
    private static bool CanMoveTo(MailLabel label) =>
        label.Role != LabelRole.Sent && label.Role != LabelRole.Drafts
        && (label.IsExclusive || label.Kind == LabelKind.User || label.Role == LabelRole.Inbox);

    private sealed record State(Mailbox Mailbox, IReadOnlyList<MailMessage> Messages, IReadOnlyList<MailLabel> Labels)
    {
        public MailLabel? Label(Guid? id) => Labels.FirstOrDefault(l => l.Id == id);
    }
}
