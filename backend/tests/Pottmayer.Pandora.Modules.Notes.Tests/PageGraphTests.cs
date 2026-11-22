using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class PageGraphTests
{
    // Graph:  a -> b -> c ,  d -> a ,  e alone
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid D = Guid.NewGuid();
    private static readonly Guid E = Guid.NewGuid();

    private static readonly (Guid Source, Guid Target)[] Edges = [(A, B), (B, C), (D, A)];

    [Fact]
    public void Depth_one_reaches_the_pages_one_hop_away_in_either_direction()
    {
        var neighborhood = PageGraph.Neighborhood(A, Edges, depth: 1);

        // b because a links to it, d because it links to a.
        Assert.Equal(3, neighborhood.Count);
        Assert.Contains(B, neighborhood);
        Assert.Contains(D, neighborhood);
        Assert.DoesNotContain(C, neighborhood);
    }

    [Fact]
    public void Depth_two_reaches_one_hop_further()
    {
        Assert.Contains(C, PageGraph.Neighborhood(A, Edges, depth: 2));
    }

    [Fact]
    public void Depth_beyond_the_graph_adds_nothing_and_never_reaches_a_disconnected_page()
    {
        var neighborhood = PageGraph.Neighborhood(A, Edges, depth: 99);

        Assert.Equal(4, neighborhood.Count);
        Assert.DoesNotContain(E, neighborhood);
    }

    [Fact]
    public void A_cycle_terminates_the_walk()
    {
        // a -> b -> c -> a: without a visited set this would not stop.
        (Guid, Guid)[] cyclic = [(A, B), (B, C), (C, A)];

        Assert.Equal(3, PageGraph.Neighborhood(A, cyclic, depth: 99).Count);
    }

    [Fact]
    public void A_page_with_no_edges_is_its_own_neighborhood()
    {
        Assert.Equal(E, Assert.Single(PageGraph.Neighborhood(E, Edges, depth: 3)));
    }
}
