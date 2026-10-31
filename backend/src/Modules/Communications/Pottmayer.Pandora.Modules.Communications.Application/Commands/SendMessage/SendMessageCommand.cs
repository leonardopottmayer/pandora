using Pottmayer.Pandora.Modules.Communications.Application.Mime;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.SendMessage;

/// <param name="SendKey">Made by the form when it opens. A key already handled is refused, so a retry cannot send twice.</param>
/// <param name="ReplyToId">The message replied to: gives the thread and the threading headers.</param>
/// <param name="ForwardOfId">The message forwarded: <paramref name="KeepAttachmentIds"/> says which of its files go along.</param>
public sealed record SendMessageInput(
    Guid UserId,
    Guid MailboxId,
    Guid SendKey,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    Guid? ReplyToId,
    Guid? ForwardOfId,
    IReadOnlyList<string> KeepAttachmentIds,
    IReadOnlyList<OutgoingAttachment> Attachments);

public sealed record SentMessageDto(bool CopySaved);

public sealed class SendMessageCommand(SendMessageInput input) : CommandBase<SendMessageInput, SentMessageDto>(input);
