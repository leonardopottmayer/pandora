using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.OpenMessage;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Pandora.Modules.Communications.Application.Queries.GetMailboxes;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SendMessage;

/// <remarks>
/// Everything that can be refused is checked before the key is taken and the mail handed over. Sending holds no gate: a
/// slow SMTP server must not hold a mailbox's actions back.
/// </remarks>
public sealed class SendMessageCommandHandler(
    IUnitOfWorkFactory factory,
    IServiceProvider services,
    SendKeys sendKeys,
    MailboxChangesStep changes,
    TimeProvider timeProvider,
    ILogger<SendMessageCommandHandler> logger) : CommandHandlerBase<SendMessageCommand, SentMessageDto>
{
    internal const int MaxRecipients = 100;

    /// <summary>Attachments of one message. Gmail's limit as the plan has it; Google's API pages state none.</summary>
    internal const long MaxAttachmentBytes = 25 * 1024 * 1024;

    protected override async Task<Result<SentMessageDto>> HandleAsync(SendMessageCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var recipients = input.To.Concat(input.Cc).Concat(input.Bcc).ToList();
        if (recipients.Count is 0 or > MaxRecipients || !recipients.All(MessageComposer.IsValidAddress))
            return Fail(MailErrors.RecipientsInvalid);
        if (input.SendKey == Guid.Empty || (input.ReplyToId is not null && input.ForwardOfId is not null))
            return Fail(MailErrors.ActionNotSupported);

        var mailbox = await MailboxReads.FindAsync(factory, input.UserId, input.MailboxId, ct);
        if (mailbox is null)
            return Fail(MailErrors.MailboxNotFound);

        var provider = services.GetRequiredKeyedService<IMailboxProvider>(mailbox.Provider.Value);

        var attachments = input.Attachments.ToList();
        string? inReplyTo = null;
        IReadOnlyList<string> references = [];
        string? threadKey = null;

        if (input.ReplyToId is { } replyToId)
        {
            var original = await ReadOriginalAsync(input.UserId, mailbox, replyToId, provider, ct);
            if (original.IsFailure)
                return Result<SentMessageDto>.Failure(original.Errors);

            var draft = MessageComposer.DraftFrom(original.Value.Raw, mailbox.Address, ComposeMode.Reply);
            (inReplyTo, references, threadKey) = (draft.InReplyTo, draft.References, original.Value.Message.ThreadKey);
        }
        else if (input.ForwardOfId is { } forwardOfId)
        {
            var original = await ReadOriginalAsync(input.UserId, mailbox, forwardOfId, provider, ct);
            if (original.IsFailure)
                return Result<SentMessageDto>.Failure(original.Errors);

            foreach (var id in input.KeepAttachmentIds)
            {
                if (MimeReader.FindAttachment(original.Value.Raw, id) is not { } part)
                    return Fail(MailErrors.AttachmentNotFound);
                attachments.Add(new OutgoingAttachment(part.FileName ?? "attachment", part.ContentType, part.Content));
            }
        }

        if (attachments.Sum(a => a.Content.LongLength) > MaxAttachmentBytes)
            return Fail(MailErrors.MessageTooLarge);

        var raw = MessageComposer.Build(new OutgoingMessage(
            // No display name: the mailbox's is the user's label for it ("Work"), not who is writing.
            From: new MailAddress(null, mailbox.Address),
            To: input.To.Select(a => new MailAddress(null, a)).ToList(),
            Cc: input.Cc.Select(a => new MailAddress(null, a)).ToList(),
            Bcc: input.Bcc.Select(a => new MailAddress(null, a)).ToList(),
            Subject: input.Subject ?? string.Empty,
            TextBody: input.TextBody ?? string.Empty,
            HtmlBody: input.HtmlBody,
            Attachments: attachments,
            MessageId: $"{input.SendKey:N}@{mailbox.Address[(mailbox.Address.IndexOf('@') + 1)..]}",
            InReplyTo: inReplyTo,
            References: references,
            Date: timeProvider.GetUtcNow()));

        if (!sendKeys.TryReserve(input.UserId, input.SendKey))
            return Fail(MailErrors.AlreadySent);

        // A thrown exception keeps the key too: nothing says the mail did not go.
        var sent = await provider.SendAsync(mailbox, raw, threadKey, ct);
        if (sent.IsFailure)
        {
            if (sent.Errors[0].Code != MailErrors.SendUncertain.Code)
                sendKeys.Release(input.UserId, input.SendKey);
            return Result<SentMessageDto>.Failure(sent.Errors);
        }

        // The mail has gone, so nothing from here on may look like a failure to send.
        try
        {
            var updated = await changes.RunAsync(mailbox.Id, provider, ct);
            if (updated.IsFailure)
                logger.LogWarning("Mailbox {MailboxId} not refreshed after a send: {Error}.", mailbox.Id, updated.Errors[0].Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Mailbox {MailboxId} not refreshed after a send.", mailbox.Id);
        }

        return Ok(new SentMessageDto(sent.Value.CopySaved));
    }

    private async Task<Result<(MailMessage Message, byte[] Raw)>> ReadOriginalAsync(
        Guid userId, Mailbox mailbox, Guid messageId, IMailboxProvider provider, CancellationToken ct)
    {
        var found = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (ctx, token) =>
            MessageReads.FindAsync(ctx, userId, messageId, token), cancellationToken: ct);
        if (found is not { } pair || pair.Mailbox.Id != mailbox.Id)
            return Result<(MailMessage, byte[])>.Failure(MailErrors.MessageNotFound);

        var raw = await provider.GetRawMessageAsync(mailbox, pair.Message.ProviderMessageId, ct);
        return raw.IsFailure
            ? Result<(MailMessage, byte[])>.Failure(raw.Errors)
            : Result<(MailMessage, byte[])>.Success((pair.Message, raw.Value));
    }
}
