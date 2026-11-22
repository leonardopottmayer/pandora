using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

/// <summary>Whether a mailbox syncs. <c>needs-reconnect</c> waits for the user; <c>error</c> keeps retrying.</summary>
public sealed record MailboxStatus : IDomainValue<MailboxStatus>
{
    public string Value { get; }

    private MailboxStatus(string value) => Value = value;

    public static readonly MailboxStatus Active = new("active");
    public static readonly MailboxStatus Syncing = new("syncing");
    public static readonly MailboxStatus NeedsReconnect = new("needs-reconnect");
    public static readonly MailboxStatus Error = new("error");

    public static readonly IReadOnlyList<MailboxStatus> All = [Active, Syncing, NeedsReconnect, Error];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static MailboxStatus FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported mailbox status value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
