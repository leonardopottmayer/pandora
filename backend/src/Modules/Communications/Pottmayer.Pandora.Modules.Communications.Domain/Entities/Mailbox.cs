using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Entities;

/// <summary>
/// One connected mailbox. The credential is an Integrations account (<see cref="ExternalAccountId"/>);
/// the server settings are set only for <c>titan</c> and <c>imap</c>.
/// </summary>
/// <remarks>
/// <c>syncing</c> while the backfill runs, then <c>active</c>. Failures count up to
/// <see cref="MaxConsecutiveFailures"/>, then <c>error</c> (still retried). A refused credential is
/// <c>needs-reconnect</c>, which the sync job skips until the user reconnects.
/// </remarks>
public sealed class Mailbox : IAuditable
{
    public const int AddressMaxLength = 320;
    public const int DisplayNameMaxLength = 100;
    public const int HostMaxLength = 255;
    public const int MaxConsecutiveFailures = 5;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public MailProvider Provider { get; private set; } = MailProvider.Imap;
    public Guid ExternalAccountId { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }

    public string? ImapHost { get; private set; }
    public int? ImapPort { get; private set; }
    public string? SmtpHost { get; private set; }
    public int? SmtpPort { get; private set; }

    public MailboxStatus Status { get; private set; } = MailboxStatus.Syncing;

    /// <summary>Opaque to everything but the provider's adapter (Gmail <c>historyId</c>).</summary>
    public string? SyncCursor { get; private set; }

    /// <summary>Where the backfill resumes (Gmail <c>pageToken</c>, IMAP lowest UID per folder).</summary>
    public string? BackfillCursor { get; private set; }

    public DateTimeOffset? BackfillCompletedAt { get; private set; }

    /// <summary>The provider's message count at backfill start, for the progress bar.</summary>
    public int? MessageTotalEstimate { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }
    public string? LastError { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Mailbox() { }

    /// <summary>
    /// <paramref name="server"/> is ignored for Gmail, optional for Titan (its preset) and required for IMAP.
    /// The backfill starts with <see cref="StartBackfill"/>, once the provider gave its cursors.
    /// </summary>
    public static Mailbox Connect(
        Guid userId, MailProvider provider, Guid externalAccountId, string address, string? displayName,
        MailServerSettings? server = null)
    {
        server = provider.UsesServerSettings
            ? server ?? provider.Preset ?? throw new ArgumentException($"'{provider}' needs server settings.", nameof(server))
            : null;

        return new Mailbox
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Provider = provider,
            ExternalAccountId = externalAccountId,
            Address = address.Trim(),
            DisplayName = NormalizeDisplayName(displayName),
            ImapHost = server?.ImapHost,
            ImapPort = server?.ImapPort,
            SmtpHost = server?.SmtpHost,
            SmtpPort = server?.SmtpPort
        };
    }

    /// <summary><paramref name="server"/> applies only where the provider uses it; null keeps the current one.</summary>
    public void Update(string? displayName, MailServerSettings? server)
    {
        DisplayName = NormalizeDisplayName(displayName);
        if (server is null || !Provider.UsesServerSettings)
            return;

        ImapHost = server.ImapHost;
        ImapPort = server.ImapPort;
        SmtpHost = server.SmtpHost;
        SmtpPort = server.SmtpPort;
    }

    public MailServerSettings? Server =>
        ImapHost is null ? null : new MailServerSettings(ImapHost, ImapPort!.Value, SmtpHost!, SmtpPort!.Value);

    public bool IsBackfillComplete => BackfillCompletedAt is not null;

    /// <summary>The sync job skips a mailbox waiting for the user to reconnect it.</summary>
    public bool CanSync => Status != MailboxStatus.NeedsReconnect;

    /// <summary>Also starts over: a cursor the provider no longer knows means reading the mailbox again.</summary>
    public void StartBackfill(string syncCursor, string? backfillCursor, int? totalEstimate, DateTimeOffset now)
    {
        SyncCursor = syncCursor;
        MessageTotalEstimate = totalEstimate;
        BackfillCompletedAt = null;
        Status = MailboxStatus.Syncing;
        AdvanceBackfill(backfillCursor, now);
    }

    /// <summary>A null cursor means the last batch was read.</summary>
    public void AdvanceBackfill(string? nextCursor, DateTimeOffset now)
    {
        BackfillCursor = nextCursor;
        if (nextCursor is null)
        {
            BackfillCompletedAt = now;
            if (Status == MailboxStatus.Syncing)
                Status = MailboxStatus.Active;
        }
    }

    public void SetSyncCursor(string syncCursor) => SyncCursor = syncCursor;

    public void RecordSuccess(DateTimeOffset now)
    {
        LastSyncedAt = now;
        LastError = null;
        ConsecutiveFailures = 0;
        Status = IsBackfillComplete ? MailboxStatus.Active : MailboxStatus.Syncing;
    }

    public void RecordFailure(string error)
    {
        LastError = error;
        ConsecutiveFailures++;
        if (ConsecutiveFailures >= MaxConsecutiveFailures)
            Status = MailboxStatus.Error;
    }

    public void MarkNeedsReconnect(string error)
    {
        LastError = error;
        Status = MailboxStatus.NeedsReconnect;
    }

    /// <summary>The credential works again: back to syncing from where it stopped.</summary>
    public void Reconnect()
    {
        if (Status != MailboxStatus.NeedsReconnect)
            return;

        LastError = null;
        ConsecutiveFailures = 0;
        Status = IsBackfillComplete ? MailboxStatus.Active : MailboxStatus.Syncing;
    }

    private static string? NormalizeDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
}
