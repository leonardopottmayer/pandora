using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A markdown page owned by the user (nte001) — the aggregate root of the Notes module. Pages form a
/// tree through <see cref="ParentId"/> (the sidebar hierarchy); the wiki-link graph is a separate
/// concern that arrives in a later phase. <see cref="Slug"/> is derived from the title at creation and
/// stays fixed afterwards, so links to a page survive renames. Archiving hides a page from the default
/// tree while keeping it editable; deleting is soft (<see cref="DeletedAt"/>), so history is preserved.
/// </summary>
public sealed class Page : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    /// <summary>The <see cref="Space"/> the page lives in. A page and its parent always share one.</summary>
    public Guid SpaceId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ContentMarkdown { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public int OrderIndex { get; private set; }
    public bool IsFavorite { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Latest immutable version number, also used for optimistic concurrency.</summary>
    public long HistoryNumber { get; private set; }
    public string HistorySource { get; private set; } = PageVersionSource.Edit;
    public long? RestoredFrom { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;
    public bool IsDeleted => DeletedAt is not null;

    private Page() { }

    public void SetHistorySource(string source) => HistorySource = source;
    public void ClearHistorySource()
    {
        HistorySource = PageVersionSource.Edit;
        RestoredFrom = null;
    }

    public PageVersion RecordVersion(TimeProvider timeProvider, bool isNew)
    {
        HistoryNumber++;
        return PageVersion.Capture(this, HistoryNumber, timeProvider,
            isNew && HistorySource == PageVersionSource.Edit ? PageVersionSource.Create : HistorySource, RestoredFrom);
    }

    /// <summary>The application validates the destination and moves descendants before this runs.</summary>
    public void Restore(PageSnapshot snapshot, long version)
    {
        Title = snapshot.Title;
        ContentMarkdown = snapshot.ContentMarkdown;
        Icon = snapshot.Icon;
        SpaceId = snapshot.SpaceId;
        ParentId = snapshot.ParentId;
        OrderIndex = snapshot.OrderIndex;
        IsFavorite = snapshot.IsFavorite;
        ArchivedAt = snapshot.ArchivedAt;
        // Deletion is a separate lifecycle operation, never replayed by restoring content.
        HistorySource = PageVersionSource.Restore;
        RestoredFrom = version;
    }

    /// <summary>
    /// Creates a page. The caller resolves a per-user-unique <paramref name="slug"/> (usually via
    /// <see cref="Slugger"/> plus a collision check) before this runs.
    /// </summary>
    public static Page Create(
        Guid userId,
        Guid spaceId,
        string title,
        string slug,
        Guid? parentId,
        string? icon,
        int orderIndex,
        string contentMarkdown,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SpaceId = spaceId,
            ParentId = parentId,
            Title = title.Trim(),
            Slug = slug,
            Icon = icon,
            OrderIndex = orderIndex,
            ContentMarkdown = contentMarkdown,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Renames the page, re-sets its icon, and replaces its markdown body (the autosave path).</summary>
    public void Update(string title, string? icon, string contentMarkdown)
    {
        Title = title.Trim();
        Icon = icon;
        ContentMarkdown = contentMarkdown;
    }

    /// <summary>
    /// Reparents and repositions the page in the tree. Guards only the trivial self-parent case; the
    /// deeper "no cycle" invariant needs the whole tree and is enforced by the caller
    /// (see <see cref="PageHierarchy.WouldCreateCycle"/>).
    /// </summary>
    public void Move(Guid? newParentId, int orderIndex)
    {
        ParentId = newParentId == Id ? null : newParentId;
        OrderIndex = orderIndex;
    }

    /// <summary>
    /// Puts the page in another space. The caller moves the whole subtree, so a page never sits in a
    /// different space from its parent.
    /// </summary>
    public void MoveToSpace(Guid spaceId) => SpaceId = spaceId;

    /// <summary>Flags or unflags the page as a favorite.</summary>
    public void SetFavorite(bool isFavorite) => IsFavorite = isFavorite;

    /// <summary>Hides the page from the default tree while keeping it editable. No-op if already archived.</summary>
    public void Archive(TimeProvider timeProvider)
    {
        if (IsArchived) return;
        ArchivedAt = timeProvider.GetUtcNow();
    }

    /// <summary>Brings an archived page back into the active tree.</summary>
    public void Unarchive() => ArchivedAt = null;

    /// <summary>Soft-deletes the page, preserving its row and history. No-op if already deleted.</summary>
    public void Delete(TimeProvider timeProvider)
    {
        if (IsDeleted) return;
        DeletedAt = timeProvider.GetUtcNow();
    }
}
