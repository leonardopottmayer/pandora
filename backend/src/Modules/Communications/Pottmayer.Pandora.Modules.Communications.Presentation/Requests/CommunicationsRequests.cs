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

/// <param name="ImapHost">The four server fields go together; none keeps the current servers.</param>
public sealed record UpdateMailboxRequest(
    string? DisplayName,
    string? ImapHost,
    int? ImapPort,
    string? SmtpHost,
    int? SmtpPort);
