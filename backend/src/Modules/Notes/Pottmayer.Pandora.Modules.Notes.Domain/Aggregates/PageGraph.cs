namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// Pure helpers over the wiki graph built from <see cref="PageLink"/>. Unlike the
/// <see cref="PageHierarchy"/> tree, this graph may contain cycles, so the walk keeps a visited set.
/// </summary>
public static class PageGraph
{
    /// <summary>
    /// The pages within <paramref name="depth"/> hops of <paramref name="rootId"/>, the root included.
    /// Edges are followed in **both** directions: what the local graph shows is the neighborhood of a
    /// page, and a page that links to the open one is just as much a neighbor as one it links to.
    /// A depth of zero is the root alone.
    /// </summary>
    public static HashSet<Guid> Neighborhood(
        Guid rootId, IReadOnlyCollection<(Guid Source, Guid Target)> edges, int depth)
    {
        var visited = new HashSet<Guid> { rootId };
        var frontier = new HashSet<Guid> { rootId };

        for (var hop = 0; hop < depth && frontier.Count > 0; hop++)
        {
            var next = new HashSet<Guid>();

            foreach (var edge in edges)
            {
                if (frontier.Contains(edge.Source) && visited.Add(edge.Target))
                    next.Add(edge.Target);

                if (frontier.Contains(edge.Target) && visited.Add(edge.Source))
                    next.Add(edge.Source);
            }

            frontier = next;
        }

        return visited;
    }
}
