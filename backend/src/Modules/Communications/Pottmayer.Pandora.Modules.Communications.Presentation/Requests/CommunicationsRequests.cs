namespace Pottmayer.Pandora.Modules.Communications.Presentation.Requests;

/// <param name="ExternalAccountId">The Integrations account the mailbox runs on.</param>
/// <param name="ImapHost">The four server fields go together; none takes the provider's preset (Titan).</param>
public sealed record ConnectMailboxRequest(
    string? Provider,
    Guid ExternalAccountId,
    string? Address,
    string? DisplayName,
    string? ImapHost,
    int? ImapPort,
    string? SmtpHost,
    int? SmtpPort);

public sealed record LabelNameRequest(string? Name);

/// <param name="SendKey">Generated when the form opens; the same key is never sent twice.</param>
/// <param name="To">Bare addresses, one form field per address; same for Cc and Bcc.</param>
/// <param name="KeepAttachmentIds">Which of the forwarded message's files go along.</param>
public sealed record SendMessageRequest(
    Guid MailboxId,
    Guid SendKey,
    List<string>? To,
    List<string>? Cc,
    List<string>? Bcc,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    Guid? ReplyToId,
    Guid? ForwardOfId,
    List<string>? KeepAttachmentIds);

/// <param name="Action"><c>mark-read</c>, <c>mark-unread</c>, <c>star</c>, <c>unstar</c>, <c>move</c>, <c>archive</c>, <c>trash</c>, <c>add-label</c> or <c>remove-label</c>.</param>
/// <param name="LabelId">The label of <c>move</c>, <c>add-label</c> and <c>remove-label</c>.</param>
public sealed record MessageActionRequest(Guid MailboxId, IReadOnlyList<Guid>? MessageIds, string? Action, Guid? LabelId);

/// <param name="ImapHost">The four server fields go together; none keeps the current servers.</param>
public sealed record UpdateMailboxRequest(
    string? DisplayName,
    string? ImapHost,
    int? ImapPort,
    string? SmtpHost,
    int? SmtpPort);
