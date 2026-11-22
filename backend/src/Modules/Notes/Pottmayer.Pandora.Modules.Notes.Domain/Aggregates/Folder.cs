using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A folder of a space (nte012): it holds other folders and pages, never the other way round — a page
/// holds only sub-pages. A folder and its parent always share a space. Deleting is soft and takes what
/// the folder holds with it.
/// </summary>
public sealed class Folder : AggregateRoot<Guid>, IAuditable
{
    public const int MaxNameLength = 255;

    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public string? Description { get; private set; }
    public int OrderIndex { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Folder() { }

    public static Folder Create(
        Guid userId, Guid spaceId, Guid? parentId, string name, string? icon, string? description, int orderIndex,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SpaceId = spaceId,
            ParentId = parentId,
            Name = name.Trim(),
            Icon = Blank(icon),
            Description = Blank(description),
            OrderIndex = orderIndex,
            CreatedAt = timeProvider.GetUtcNow()
        };

    public void Update(string name, string? icon, string? description)
    {
        Name = name.Trim();
        Icon = Blank(icon);
        Description = Blank(description);
    }

    /// <summary>The caller checks the new parent is the owner's, in the same space, and not inside this folder.</summary>
    public void Move(Guid? parentId, int orderIndex)
    {
        ParentId = parentId == Id ? null : parentId;
        OrderIndex = orderIndex;
    }

    public void Delete(TimeProvider timeProvider) => DeletedAt ??= timeProvider.GetUtcNow();

    /// <summary>The folder plus every folder beneath it among <paramref name="all"/>.</summary>
    public static IReadOnlyList<Folder> Subtree(Guid rootId, IReadOnlyList<Folder> all)
    {
        var byParent = all.ToLookup(f => f.ParentId);
        var result = new List<Folder>();
        var queue = new Queue<Guid>([rootId]);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (all.FirstOrDefault(f => f.Id == id) is not { } folder)
                continue;
            result.Add(folder);
            foreach (var child in byParent[id])
                queue.Enqueue(child.Id);
        }
        return result;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
