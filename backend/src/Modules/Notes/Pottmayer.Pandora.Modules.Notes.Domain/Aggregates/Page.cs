using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A page of blocks owned by the user (nte001) — the aggregate root of the Notes module. Pages form a
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
    /// <summary>The <see cref="Folder"/> a top-level page sits in; a sub-page has none (it lives under its parent).</summary>
    public Guid? FolderId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    /// <summary>The block document (<see cref="PageContent"/>), as stored.</summary>
    public string Content { get; private set; } = PageContent.Empty.Json;
    /// <summary>The document's readable text, kept beside it for the search vector and excerpts.</summary>
    public string ContentText { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public int OrderIndex { get; private set; }
    public bool IsFavorite { get; private set; }
    public bool IsTemplate { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    /// <summary>How the page looks (<see cref="PageAppearance"/>), as stored.</summary>
    public string Appearance { get; private set; } = PageAppearance.Default.ToJson();
    /// <summary>Locked, the page opens to read: its blocks are not edited until it is unlocked.</summary>
    public bool IsLocked { get; private set; }
    /// <summary>When someone last said the page is right; <see cref="VerifiedUntil"/> is when that runs out.</summary>
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? VerifiedUntil { get; private set; }
    /// <summary>When the page was last opened, for the recent pages.</summary>
    public DateTimeOffset? ViewedAt { get; private set; }
    /// <summary>The token of the page's public link; null while it is private.</summary>
    public string? PublicToken { get; private set; }

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
        SetContent(PageContent.Parse(snapshot.Content));
        Icon = snapshot.Icon;
        SpaceId = snapshot.SpaceId;
        ParentId = snapshot.ParentId;
        FolderId = snapshot.ParentId is null ? snapshot.FolderId : null;
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
        PageContent content,
        TimeProvider timeProvider,
        Guid? folderId = null)
    {
        var page = new Page
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SpaceId = spaceId,
            ParentId = parentId,
            FolderId = parentId is null ? folderId : null,
            Title = title.Trim(),
            Slug = slug,
            Icon = icon,
            OrderIndex = orderIndex,
            CreatedAt = timeProvider.GetUtcNow()
        };
        page.SetContent(content);
        return page;
    }

    /// <summary>Renames the page, re-sets its icon, and replaces its blocks (the autosave path).</summary>
    public void Update(string title, string? icon, PageContent content)
    {
        Title = title.Trim();
        Icon = icon;
        SetContent(content);
    }

    /// <summary>Makes this page available as a template, or returns it to an ordinary page.</summary>
    public void SetTemplate(bool isTemplate) => IsTemplate = isTemplate;

    private void SetContent(PageContent content)
    {
        Content = content.Json;
        ContentText = content.Text;
    }

    /// <summary>
    /// Reparents and repositions the page in the tree. Guards only the trivial self-parent case; the
    /// deeper "no cycle" invariant needs the whole tree and is enforced by the caller
    /// (see <see cref="PageHierarchy.WouldCreateCycle"/>).
    /// </summary>
    public void Move(Guid? newParentId, int orderIndex, Guid? folderId = null)
    {
        ParentId = newParentId == Id ? null : newParentId;
        // Under a page or in a folder, never both: a sub-page lives where its parent does.
        FolderId = ParentId is null ? folderId : null;
        OrderIndex = orderIndex;
    }

    /// <summary>
    /// Puts the page in another space. The caller moves the whole subtree, so a page never sits in a
    /// different space from its parent.
    /// </summary>
    public void MoveToSpace(Guid spaceId) => SpaceId = spaceId;

    /// <summary>Takes the page out of its folder, to the top of its space (its folder went away).</summary>
    public void LeaveFolder() => FolderId = null;

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

    public void SetAppearance(PageAppearance appearance) => Appearance = appearance.ToJson();

    public void SetLocked(bool locked) => IsLocked = locked;

    /// <summary>Marks the page as checked now, valid until <paramref name="until"/> (null: it does not run out).</summary>
    public void Verify(DateTimeOffset? until, TimeProvider timeProvider)
    {
        VerifiedAt = timeProvider.GetUtcNow();
        VerifiedUntil = until;
    }

    public void ClearVerification()
    {
        VerifiedAt = null;
        VerifiedUntil = null;
    }

    /// <summary>Opens the public link with <paramref name="token"/>, or closes it with null.</summary>
    public void SetPublicToken(string? token) => PublicToken = token;

    /// <summary>
    /// Brings a deleted page back from the trash, under <paramref name="parentId"/> or at the top of
    /// <paramref name="spaceId"/> (in <paramref name="folderId"/>), with a slug that is free again.
    /// </summary>
    public void RestoreFromTrash(Guid spaceId, Guid? parentId, Guid? folderId, string slug)
    {
        DeletedAt = null;
        SpaceId = spaceId;
        ParentId = parentId;
        FolderId = parentId is null ? folderId : null;
        Slug = slug;
    }

    /// <summary>
    /// Soft-deletes the page at <paramref name="at"/>, preserving its row and history. No-op if already
    /// deleted. Pages deleted together share the moment: the trash brings them back together.
    /// </summary>
    public void Delete(DateTimeOffset at)
    {
        if (IsDeleted) return;
        DeletedAt = at;
    }
}
