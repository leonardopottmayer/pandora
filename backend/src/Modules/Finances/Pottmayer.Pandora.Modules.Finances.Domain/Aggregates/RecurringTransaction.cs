using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

public sealed class RecurringTransaction : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    // template
    public Guid? AccountId { get; private set; }
    public Guid? CardId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public decimal? Amount { get; private set; }
    public bool AmountIsEstimate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? Payee { get; private set; }
    public Guid? SystemCategoryId { get; private set; }
    public Guid? UserCategoryId { get; private set; }

    // rule fields (stored flat; logic in RecurrenceRule)
    public string Frequency { get; private set; } = string.Empty;
    public short Interval { get; private set; }
    public short? DayOfMonth { get; private set; }
    public short? Weekday { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int? MaxOccurrences { get; private set; }

    // execution
    public string Status { get; private set; } = "active";
    public bool AutoPost { get; private set; }
    public DateOnly NextOccurrenceOn { get; private set; }
    public int OccurrencesCount { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsActive => Status == "active";
    public bool IsPaused => Status == "paused";
    public bool IsFinished => Status == "finished";

    private RecurringTransaction() { }

    public static RecurringTransaction Create(
        Guid userId,
        string name,
        Guid? accountId,
        Guid? cardId,
        string kind,
        decimal? amount,
        bool amountIsEstimate,
        string description,
        string? payee,
        Guid? systemCategoryId,
        Guid? userCategoryId,
        string frequency,
        short interval,
        short? dayOfMonth,
        short? weekday,
        DateOnly startDate,
        DateOnly? endDate,
        int? maxOccurrences,
        bool autoPost,
        TimeProvider timeProvider)
    {
        return new RecurringTransaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            AccountId = accountId,
            CardId = cardId,
            Kind = kind,
            Amount = amount,
            AmountIsEstimate = amountIsEstimate,
            Description = description.Trim(),
            Payee = payee,
            SystemCategoryId = systemCategoryId,
            UserCategoryId = userCategoryId,
            Frequency = frequency,
            Interval = interval,
            DayOfMonth = dayOfMonth,
            Weekday = weekday,
            StartDate = startDate,
            EndDate = endDate,
            MaxOccurrences = maxOccurrences,
            Status = "active",
            AutoPost = autoPost,
            NextOccurrenceOn = startDate,
            OccurrencesCount = 0,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>Builds the recurrence rule value object from the stored flat fields.</summary>
    public RecurrenceRule GetRule() =>
        RecurrenceRule.Create(Frequency, Interval, DayOfMonth, Weekday, StartDate, EndDate, MaxOccurrences);

    /// <summary>
    /// Updates the non-structural template fields. The destination (account/card), frequency,
    /// interval, day anchors and start date are immutable after creation to keep past occurrences
    /// consistent — only future-facing fields (end, max, auto_post) and cosmetic fields change here.
    /// </summary>
    public void UpdateTemplate(
        string name,
        decimal? amount,
        bool amountIsEstimate,
        string description,
        string? payee,
        Guid? systemCategoryId,
        Guid? userCategoryId,
        DateOnly? endDate,
        int? maxOccurrences,
        bool autoPost)
    {
        Name = name.Trim();
        Amount = amount;
        AmountIsEstimate = amountIsEstimate;
        Description = description.Trim();
        Payee = payee;
        SystemCategoryId = systemCategoryId;
        UserCategoryId = userCategoryId;
        EndDate = endDate;
        MaxOccurrences = maxOccurrences;
        AutoPost = autoPost;
    }

    /// <summary>Pauses generation. No-op if already paused.</summary>
    public bool Pause()
    {
        if (Status != "active") return false;
        Status = "paused";
        return true;
    }

    /// <summary>Resumes generation. No-op unless currently paused.</summary>
    public bool Resume()
    {
        if (Status != "paused") return false;
        Status = "active";
        return true;
    }

    /// <summary>
    /// Advances the cursor after successfully generating for <paramref name="generatedDate"/>.
    /// If the resulting next date is terminated, also finishes the recurrence.
    /// </summary>
    public void AdvanceCursor(DateOnly generatedDate)
    {
        OccurrencesCount++;
        var rule = GetRule();
        var next = rule.NextOccurrenceAfter(generatedDate);
        NextOccurrenceOn = next;
        if (rule.IsTerminated(next, OccurrencesCount))
            Status = "finished";
    }
}
