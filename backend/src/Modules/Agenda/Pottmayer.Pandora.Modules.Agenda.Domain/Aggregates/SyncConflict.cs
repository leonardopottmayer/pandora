using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// An event or task both sides changed between two syncs (doc agd011). The newer edit won; the losing version is
/// kept as JSON so the user can see what was overwritten. A log: written once, never edited.
/// </summary>
public sealed class SyncConflict : AggregateRoot<Guid>
{
    private const int MaxTitleLength = 200;

    public Guid UserId { get; private set; }
    public Guid BindingId { get; private set; }
    public SyncConflictSubject SubjectType { get; private set; } = SyncConflictSubject.Event;
    public Guid SubjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public SyncConflictSide LosingSide { get; private set; } = SyncConflictSide.Google;
    public string LosingVersion { get; private set; } = "{}";
    public DateTimeOffset DetectedAt { get; private set; }

    private SyncConflict() { }

    public static SyncConflict Create(
        Guid userId, Guid bindingId, SyncConflictSubject subjectType, Guid subjectId, string title,
        SyncConflictSide losingSide, string losingVersionJson, TimeProvider timeProvider) => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BindingId = bindingId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Title = title.Length <= MaxTitleLength ? title : title[..MaxTitleLength],
            LosingSide = losingSide,
            LosingVersion = losingVersionJson,
            DetectedAt = timeProvider.GetUtcNow()
        };
}
