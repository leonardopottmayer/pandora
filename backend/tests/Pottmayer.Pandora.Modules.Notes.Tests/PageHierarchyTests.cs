using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class PageHierarchyTests
{
    // Tree:  a (root) -> b -> c
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static readonly IReadOnlyDictionary<Guid, Guid?> Parents = new Dictionary<Guid, Guid?>
    {
        [A] = null,
        [B] = A,
        [C] = B
    };

    [Fact]
    public void Moving_under_own_descendant_is_a_cycle()
    {
        // a -> c would put a beneath its own grandchild.
        Assert.True(PageHierarchy.WouldCreateCycle(A, C, Parents));
    }

    [Fact]
    public void Moving_under_self_is_a_cycle()
    {
        Assert.True(PageHierarchy.WouldCreateCycle(B, B, Parents));
    }

    [Fact]
    public void Moving_to_an_unrelated_branch_is_allowed()
    {
        // c -> a is fine: a is not a descendant of c.
        Assert.False(PageHierarchy.WouldCreateCycle(C, A, Parents));
    }

    [Fact]
    public void Moving_to_root_is_allowed()
    {
        Assert.False(PageHierarchy.WouldCreateCycle(C, null, Parents));
    }
}
