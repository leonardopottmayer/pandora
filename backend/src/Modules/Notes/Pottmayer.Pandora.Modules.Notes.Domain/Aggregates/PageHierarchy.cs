namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// Pure helpers over the page tree. The tree lives in <see cref="Page.ParentId"/>; cycle prevention on
/// reparent needs the whole set of parent links, which the caller supplies as a map.
/// </summary>
public static class PageHierarchy
{
    /// <summary>
    /// Whether attaching <paramref name="movingId"/> under <paramref name="newParentId"/> would create a
    /// cycle — i.e. the new parent is the page itself or one of its own descendants. Walking upward from
    /// the new parent, a cycle exists if we ever reach the moving page. <paramref name="parents"/> maps
    /// every page id to its current parent id (roots map to <c>null</c>).
    /// </summary>
    public static bool WouldCreateCycle(
        Guid movingId, Guid? newParentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        for (var current = newParentId; current is not null;)
        {
            if (current == movingId)
                return true;

            // A broken/foreign link ends the walk without a cycle.
            if (!parents.TryGetValue(current.Value, out var next))
                return false;

            current = next;
        }

        return false;
    }
}
