using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>The user's Notes settings and the stable identity of the daily-notes folder.</summary>
public sealed class NotesPreferences : IAuditable
{
    public Guid UserId { get; private set; }
    public Guid? DailyTemplateId { get; private set; }
    public Guid? DailyFolderId { get; private set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private NotesPreferences() { }
    public static NotesPreferences Create(Guid userId) => new() { UserId = userId };
    public void SetDailyTemplate(Guid? pageId) => DailyTemplateId = pageId;
    public void SetDailyFolder(Guid folderId) => DailyFolderId = folderId;
}
