using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Domain.Entities;

/// <summary>
/// One walk of one root by its agent, from <c>running</c> to <c>completed</c> (applied), <c>aborted</c>
/// (discarded) or <c>held</c> (the safety brake fired; waits for the user).
/// </summary>
public sealed class Scan
{
    public const int ErrorMaxLength = 200;

    /// <summary>Above this share of the root going missing at once, the scan is held for the user.</summary>
    public const double SafetyBrakeShare = 0.2;

    public Guid Id { get; private set; }
    public Guid RootId { get; private set; }
    public ScanStatus Status { get; private set; } = ScanStatus.Running;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? LastBatchAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Distinct entries the agent reported.</summary>
    public int Seen { get; private set; }
    public int Created { get; private set; }
    public int Changed { get; private set; }
    public int Moved { get; private set; }
    public int Missing { get; private set; }
    public int Excluded { get; private set; }
    public string? Error { get; private set; }

    private Scan() { }

    public static Scan Start(Guid rootId, DateTimeOffset now)
        => new() { Id = Guid.CreateVersion7(), RootId = rootId, StartedAt = now };

    public bool IsRunning => Status == ScanStatus.Running;
    public bool IsHeld => Status == ScanStatus.Held;

    public void RecordBatch(DateTimeOffset now, int seen, int created, int changed)
    {
        LastBatchAt = now;
        Seen += seen;
        Created += created;
        Changed += changed;
    }

    /// <summary>Whether marking <paramref name="missing"/> of <paramref name="presentBefore"/> entries missing must wait for the user.</summary>
    public static bool TripsSafetyBrake(int missing, int presentBefore) =>
        missing > 0 && missing > presentBefore * SafetyBrakeShare;

    public void Complete(DateTimeOffset now, int moved, int missing, int excluded)
    {
        Moved += moved;
        Missing = missing;
        Excluded = excluded;
        Finish(ScanStatus.Completed, now, null);
    }

    /// <summary>Moves are applied; missing and excluded are only counted, to show the user what confirming would do.</summary>
    public void Hold(DateTimeOffset now, int moved, int missing, int excluded)
    {
        Moved += moved;
        Missing = missing;
        Excluded = excluded;
        Finish(ScanStatus.Held, now, null);
    }

    public void Abort(DateTimeOffset now, string reason) =>
        Finish(ScanStatus.Aborted, now, reason.Length > ErrorMaxLength ? reason[..ErrorMaxLength] : reason);

    private void Finish(ScanStatus status, DateTimeOffset now, string? error)
    {
        Status = status;
        FinishedAt = now;
        Error = error;
    }
}
