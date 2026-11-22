using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// "Notify me about <em>subject</em> with <em>this offset</em>, on <em>these channels</em>." The
/// polymorphic scheduling primitive of the module (doc agd007): one row per wanted ping, keyed to a
/// subject by <see cref="SubjectType"/> + <see cref="SubjectId"/> without an FK (validated in the
/// application, removed with the subject).
///
/// <para>Phase 3 wires only <see cref="AlertSubjectType.Task"/>. The firing instant is the subject's
/// anchor plus <see cref="OffsetMinutes"/> — for a task, its <c>DueAt</c>. Idempotency of firing lives
/// in <see cref="AlertDispatch"/>.</para>
/// </summary>
public sealed class Alert : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public AlertSubjectType SubjectType { get; private set; }
    public Guid SubjectId { get; private set; }

    /// <summary>Signed, relative to the subject anchor. <c>0</c> = at the instant, <c>-15</c> = fifteen minutes before.</summary>
    public int OffsetMinutes { get; private set; }

    /// <summary>Null ⇒ resolve from the user's preference in Channels. Otherwise the explicit channels.</summary>
    public string[]? Channels { get; private set; }

    public bool IsEnabled { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Alert() { }

    public static Alert Create(
        Guid userId,
        AlertSubjectType subjectType,
        Guid subjectId,
        int offsetMinutes,
        string[]? channels,
        TimeProvider timeProvider) => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            OffsetMinutes = offsetMinutes,
            Channels = channels is { Length: > 0 } ? channels : null,
            IsEnabled = true,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Copies this alert onto a new subject of the same type — used when a recurring task spawns its next instance.</summary>
    public Alert CopyFor(Guid subjectId, TimeProvider timeProvider) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = UserId,
        SubjectType = SubjectType,
        SubjectId = subjectId,
        OffsetMinutes = OffsetMinutes,
        Channels = Channels,
        IsEnabled = IsEnabled,
        CreatedAt = timeProvider.GetUtcNow()
    };

    /// <summary>The firing instant for a given subject anchor.</summary>
    public DateTimeOffset FiringInstant(DateTimeOffset anchor) => anchor.AddMinutes(OffsetMinutes);
}
