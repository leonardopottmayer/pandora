namespace Pottmayer.Pandora.Modules.Communications.Abstractions;

/// <summary>Configuration for the Communications module (bound from the <c>Pandora:Communications</c> section).</summary>
public sealed class CommunicationsOptions
{
    public const string SectionName = "Pandora:Communications";

    /// <summary>How often the sync job walks every mailbox.</summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Messages read per backfill batch.</summary>
    public int BackfillBatchSize { get; set; } = 500;

    /// <summary>
    /// How long one cycle keeps reading backfill batches of a mailbox; at least one batch always runs.
    /// Bounds how long the job (sequential) and "Sync now" spend on a large mailbox's first download.
    /// Longer than <see cref="SyncInterval"/> on purpose: a backfill then runs almost all the time, and the
    /// timer skips the ticks a long cycle covers.
    /// </summary>
    public TimeSpan BackfillBudget { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>A cached body not opened for this many days is deleted (it is fetched again on open).</summary>
    public int ContentCacheRetentionDays { get; set; } = 30;
}
